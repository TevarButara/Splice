#!/usr/bin/env bash
set -euo pipefail
export LC_ALL=C
export LANG=C
umask 077

if [[ $# -ne 3 ]]; then
    echo "Usage: bash Backend/database/scripts/restore-c4d1.sh <bundle-directory> <new-target-database> <new-replay-root>" >&2
    exit 2
fi

bundle_directory="$1"
target_database="$2"
target_replay_root="$3"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

fail() {
    echo "C4D1_RESTORE_FAILED: $*" >&2
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

[[ -d "${bundle_directory}" && ! -L "${bundle_directory}" ]] ||
    fail "bundle directory is unavailable"
[[ "${target_database}" =~ ^[A-Za-z_][A-Za-z0-9_-]{0,62}$ ]] ||
    fail "target database name is invalid"
[[ "${target_database}" != "postgres" &&
   "${target_database}" != "template0" &&
   "${target_database}" != "template1" ]] ||
    fail "system databases cannot be restore targets"
[[ "${target_replay_root}" == /* && "${target_replay_root}" != "/" ]] ||
    fail "target replay root must be an absolute non-root path"
[[ ! -e "${target_replay_root}" ]] ||
    fail "target replay root already exists"
bundle_version="$(tr -d '\r\n' < "${bundle_directory}/bundle-version")"
[[ "${bundle_version}" == "splice-c4d1-backup/v1" ]] ||
    fail "unsupported bundle version"

database_exists="$(psql -X -qAt -d postgres \
    -v target_database="${target_database}" <<'SQL'
SELECT 1 FROM pg_database WHERE datname = :'target_database';
SQL
)"
[[ -z "${database_exists}" ]] ||
    fail "target database already exists"

expected_dump_hash="$(tr -d '[:space:]' < "${bundle_directory}/database.dump.sha256")"
actual_dump_hash="$(shasum -a 256 "${bundle_directory}/database.dump" |
    awk '{print $1}')"
[[ "${actual_dump_hash}" == "${expected_dump_hash}" ]] ||
    fail "database dump checksum mismatch"

# Validate every bundled object before creating any target resource.
while IFS=$'\t' read -r key expected_etag expected_length command_hash \
    tick_count command_count outcome breached_rings simulation_version encoding; do
    [[ -z "${key}" ]] && continue
    valid_object_key "${key}" || fail "unsafe object key in bundle inventory"
    [[ "${encoding}" == "gzip" ]] || fail "unsupported replay encoding for ${key}"
    bundled_path="${bundle_directory}/replay-objects/${key}"
    [[ -f "${bundled_path}" && ! -L "${bundled_path}" ]] ||
        fail "bundled replay object is missing: ${key}"
    actual_length="$(wc -c < "${bundled_path}" | tr -d '[:space:]')"
    [[ "${actual_length}" == "${expected_length}" ]] ||
        fail "bundled replay object length mismatch: ${key}"
    actual_etag="$(shasum -a 256 "${bundled_path}" | awk '{print $1}')"
    [[ "${actual_etag}" == "${expected_etag}" ]] ||
        fail "bundled replay object checksum mismatch: ${key}"
    gzip -t "${bundled_path}" ||
        fail "bundled replay object gzip validation failed: ${key}"
done < "${bundle_directory}/replay-objects.tsv"

createdb "${target_database}"
mkdir -p "${target_replay_root}"
chmod 700 "${target_replay_root}"
pg_restore --exit-on-error --single-transaction --no-owner --no-privileges \
    --dbname="${target_database}" "${bundle_directory}/database.dump"

while IFS=$'\t' read -r key expected_etag expected_length command_hash \
    tick_count command_count outcome breached_rings simulation_version encoding; do
    [[ -z "${key}" ]] && continue
    destination="${target_replay_root}/${key}"
    mkdir -p "$(dirname "${destination}")"
    cp -p "${bundle_directory}/replay-objects/${key}" "${destination}"
done < "${bundle_directory}/replay-objects.tsv"

bash "${script_dir}/verify-c4d1-restore.sh" \
    "${target_database}" "${target_replay_root}" "${bundle_directory}"
echo "C4D1 restore complete: database=${target_database} replay_root=${target_replay_root}"
