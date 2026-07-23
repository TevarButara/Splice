#!/usr/bin/env bash
set -euo pipefail
export LC_ALL=C
export LANG=C
umask 077

if [[ $# -ne 3 ]]; then
    echo "Usage: bash Backend/database/scripts/backup-c4d1.sh <source-database> <replay-root> <new-bundle-directory>" >&2
    exit 2
fi

source_database="$1"
replay_root="$2"
bundle_directory="$3"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ops_dir="$(cd "${script_dir}/../ops" && pwd)"
runtime_dir="$(mktemp -d "${TMPDIR:-/tmp}/splice-c4d1-backup.XXXXXX")"
snapshot_pid=""
snapshot_backend_pid=""
working_bundle=""
completed=false

cleanup() {
    if [[ -n "${snapshot_backend_pid}" ]]; then
        psql -X -qAt -v ON_ERROR_STOP=1 -d "${source_database}" \
            -v target_pid="${snapshot_backend_pid}" >/dev/null 2>&1 <<'SQL' || true
SELECT pg_terminate_backend(:'target_pid'::integer)
 WHERE :'target_pid'::integer <> pg_backend_pid();
SQL
        snapshot_backend_pid=""
    fi
    if [[ -n "${snapshot_pid}" ]]; then
        if kill -0 "${snapshot_pid}" >/dev/null 2>&1; then
            kill "${snapshot_pid}" >/dev/null 2>&1 || true
        fi
        wait "${snapshot_pid}" >/dev/null 2>&1 || true
        snapshot_pid=""
    fi
    rm -rf "${runtime_dir}"
    if [[ "${completed}" != true && -n "${working_bundle}" && -d "${working_bundle}" ]]; then
        rm -rf "${working_bundle}"
    fi
}
trap cleanup EXIT INT TERM

fail() {
    echo "C4D1_BACKUP_FAILED: $*" >&2
    exit 1
}

valid_object_key() {
    local key="$1"
    [[ -n "${key}" && ${#key} -le 512 &&
       "${key}" != /* && "${key}" != *\\* && "${key}" != *//* &&
       "${key}" != "." && "${key}" != ./* &&
       "${key}" != */./* && "${key}" != */. &&
       "${key}" != ".." && "${key}" != ../* &&
       "${key}" != */../* && "${key}" != */.. ]] || return 1
    case "${key}" in
        *[!A-Za-z0-9._/-]*) return 1 ;;
    esac
}

[[ -n "${source_database}" ]] || fail "source database is required"
[[ "${replay_root}" == /* && "${replay_root}" != "/" ]] ||
    fail "replay root must be an absolute non-root path"
[[ -d "${replay_root}" && ! -L "${replay_root}" ]] ||
    fail "replay root must be an existing real directory"
[[ ! -e "${bundle_directory}" ]] ||
    fail "bundle target already exists: ${bundle_directory}"
bundle_parent="$(dirname "${bundle_directory}")"
[[ -d "${bundle_parent}" ]] ||
    fail "bundle parent does not exist: ${bundle_parent}"

symlink_path="$(find "${replay_root}" -type l -print -quit)"
if [[ -n "${symlink_path}" ]]; then
    fail "replay root contains a symlink"
fi

working_bundle="$(mktemp -d "${bundle_directory}.incomplete.XXXXXX")"
mkdir -p "${working_bundle}/replay-objects"
snapshot_file="${runtime_dir}/snapshot-id"
snapshot_log="${runtime_dir}/snapshot.log"

psql -X -qAt -v ON_ERROR_STOP=1 -d "${source_database}" >"${snapshot_log}" 2>&1 <<SQL &
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
\o '${snapshot_file}'
SELECT pg_backend_pid();
SELECT pg_export_snapshot();
\o
SELECT pg_sleep(86400);
SQL
snapshot_pid=$!

for _ in {1..100}; do
    [[ -s "${snapshot_file}" ]] && break
    if ! kill -0 "${snapshot_pid}" >/dev/null 2>&1; then
        cat "${snapshot_log}" >&2
        fail "could not export PostgreSQL snapshot"
    fi
    sleep 0.1
done
[[ -s "${snapshot_file}" ]] || fail "timed out while exporting PostgreSQL snapshot"
snapshot_backend_pid="$(sed -n '1p' "${snapshot_file}" | tr -d '[:space:]')"
snapshot_id="$(sed -n '2p' "${snapshot_file}" | tr -d '[:space:]')"
[[ "${snapshot_backend_pid}" =~ ^[0-9]+$ ]] ||
    fail "PostgreSQL returned an invalid snapshot backend identifier"
[[ "${snapshot_id}" =~ ^[0-9A-Fa-f]+-[0-9A-Fa-f]+-[0-9A-Fa-f]+$ ]] ||
    fail "PostgreSQL returned an invalid snapshot identifier"

pg_dump --format=custom --compress=6 --no-owner --no-privileges \
    --snapshot="${snapshot_id}" --file="${working_bundle}/database.dump" \
    "${source_database}"

{
    printf "BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;\n"
    printf "SET TRANSACTION SNAPSHOT '%s';\n" "${snapshot_id}"
    cat "${ops_dir}/c4d1_fingerprint.sql"
    printf "COMMIT;\n"
} | psql -X -qAt -F $'\t' -v ON_ERROR_STOP=1 -d "${source_database}" \
    >"${working_bundle}/database-fingerprint.tsv"

{
    printf "BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;\n"
    printf "SET TRANSACTION SNAPSHOT '%s';\n" "${snapshot_id}"
    cat "${ops_dir}/c4d1_replay_inventory.sql"
    printf "COMMIT;\n"
} | psql -X -qAt -F $'\t' -v ON_ERROR_STOP=1 -d "${source_database}" \
    >"${working_bundle}/replay-objects.tsv"

unsupported_count="$(psql -X -qAt -v ON_ERROR_STOP=1 -d "${source_database}" <<SQL
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET TRANSACTION SNAPSHOT '${snapshot_id}';
SELECT count(*) FROM splice.raid_replays
 WHERE storage_provider NOT IN ('postgres-jsonb', 'local-filesystem');
COMMIT;
SQL
)"
[[ "${unsupported_count}" == "0" ]] ||
    fail "this local-first backup cannot fetch s3-compatible replay objects"

snapshot_utc="$(psql -X -qAt -v ON_ERROR_STOP=1 -d "${source_database}" <<SQL
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET TRANSACTION SNAPSHOT '${snapshot_id}';
SELECT to_char(transaction_timestamp() AT TIME ZONE 'UTC',
               'YYYY-MM-DD"T"HH24:MI:SS.US"Z"');
COMMIT;
SQL
)"

psql -X -qAt -v ON_ERROR_STOP=1 -d "${source_database}" \
    -v target_pid="${snapshot_backend_pid}" >/dev/null <<'SQL'
SELECT pg_terminate_backend(:'target_pid'::integer)
 WHERE :'target_pid'::integer <> pg_backend_pid();
SQL
snapshot_backend_pid=""
if kill -0 "${snapshot_pid}" >/dev/null 2>&1; then
    kill "${snapshot_pid}" >/dev/null 2>&1 || true
fi
wait "${snapshot_pid}" >/dev/null 2>&1 || true
snapshot_pid=""

while IFS=$'\t' read -r key expected_etag expected_length command_hash \
    tick_count command_count outcome breached_rings simulation_version encoding; do
    [[ -z "${key}" ]] && continue
    valid_object_key "${key}" || fail "unsafe replay object key in database"
    [[ "${encoding}" == "gzip" ]] || fail "unsupported replay encoding for ${key}"
    source_path="${replay_root}/${key}"
    [[ -f "${source_path}" && ! -L "${source_path}" ]] ||
        fail "referenced replay object is missing: ${key}"
    actual_length="$(wc -c < "${source_path}" | tr -d '[:space:]')"
    [[ "${actual_length}" == "${expected_length}" ]] ||
        fail "replay object length mismatch: ${key}"
    actual_etag="$(shasum -a 256 "${source_path}" | awk '{print $1}')"
    [[ "${actual_etag}" == "${expected_etag}" ]] ||
        fail "replay object checksum mismatch: ${key}"
    gzip -t "${source_path}" ||
        fail "replay object gzip validation failed: ${key}"
    destination_path="${working_bundle}/replay-objects/${key}"
    mkdir -p "$(dirname "${destination_path}")"
    cp -p "${source_path}" "${destination_path}"
    copied_length="$(wc -c < "${destination_path}" | tr -d '[:space:]')"
    copied_etag="$(shasum -a 256 "${destination_path}" | awk '{print $1}')"
    [[ "${copied_length}" == "${expected_length}" &&
       "${copied_etag}" == "${expected_etag}" ]] ||
        fail "replay object changed while it was copied: ${key}"
done < "${working_bundle}/replay-objects.tsv"

printf "splice-c4d1-backup/v1\n" >"${working_bundle}/bundle-version"
date -u +"%Y-%m-%dT%H:%M:%SZ" >"${working_bundle}/created-utc"
printf "%s\n" "${snapshot_utc}" >"${working_bundle}/snapshot-utc"
shasum -a 256 "${working_bundle}/database.dump" |
    awk '{print $1}' >"${working_bundle}/database.dump.sha256"

mv "${working_bundle}" "${bundle_directory}"
working_bundle=""
completed=true
echo "C4D1 backup complete: ${bundle_directory}"
