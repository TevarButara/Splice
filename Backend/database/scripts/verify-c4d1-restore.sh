#!/usr/bin/env bash
set -euo pipefail
export LC_ALL=C
export LANG=C
umask 077

if [[ $# -ne 3 ]]; then
    echo "Usage: bash Backend/database/scripts/verify-c4d1-restore.sh <database> <replay-root> <bundle-directory>" >&2
    exit 2
fi

database="$1"
replay_root="$2"
bundle_directory="$3"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ops_dir="$(cd "${script_dir}/../ops" && pwd)"
runtime_dir="$(mktemp -d "${TMPDIR:-/tmp}/splice-c4d1-verify.XXXXXX")"
trap 'rm -rf "${runtime_dir}"' EXIT INT TERM

fail() {
    echo "C4D1_RESTORE_VERIFY_FAILED: $*" >&2
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
bundle_version="$(tr -d '\r\n' < "${bundle_directory}/bundle-version")"
[[ "${bundle_version}" == "splice-c4d1-backup/v1" ]] ||
    fail "unsupported bundle version"
[[ -d "${replay_root}" && ! -L "${replay_root}" ]] ||
    fail "replay root is unavailable"
symlink_path="$(find "${replay_root}" -type l -print -quit)"
if [[ -n "${symlink_path}" ]]; then
    fail "restored replay root contains a symlink"
fi

expected_dump_hash="$(tr -d '[:space:]' < "${bundle_directory}/database.dump.sha256")"
actual_dump_hash="$(shasum -a 256 "${bundle_directory}/database.dump" |
    awk '{print $1}')"
[[ "${actual_dump_hash}" == "${expected_dump_hash}" ]] ||
    fail "database dump checksum mismatch"

psql -X -q -v ON_ERROR_STOP=1 -d "${database}" \
    -f "${ops_dir}/c4d1_integrity.sql" >/dev/null
psql -X -qAt -F $'\t' -v ON_ERROR_STOP=1 -d "${database}" \
    -f "${ops_dir}/c4d1_fingerprint.sql" \
    >"${runtime_dir}/restored-fingerprint.tsv"
diff -u "${bundle_directory}/database-fingerprint.tsv" \
    "${runtime_dir}/restored-fingerprint.tsv" >/dev/null ||
    fail "restored database fingerprint differs from the source snapshot"
psql -X -qAt -F $'\t' -v ON_ERROR_STOP=1 -d "${database}" \
    -f "${ops_dir}/c4d1_replay_inventory.sql" \
    >"${runtime_dir}/restored-replay-objects.tsv"
diff -u "${bundle_directory}/replay-objects.tsv" \
    "${runtime_dir}/restored-replay-objects.tsv" >/dev/null ||
    fail "restored replay pointers differ from the bundle inventory"

expected_objects=0
while IFS=$'\t' read -r key expected_etag expected_length command_hash \
    tick_count command_count outcome breached_rings simulation_version encoding; do
    [[ -z "${key}" ]] && continue
    expected_objects=$((expected_objects + 1))
    valid_object_key "${key}" || fail "unsafe object key in bundle inventory"
    [[ "${encoding}" == "gzip" ]] || fail "unsupported replay encoding for ${key}"
    object_path="${replay_root}/${key}"
    [[ -f "${object_path}" && ! -L "${object_path}" ]] ||
        fail "restored replay object is missing: ${key}"
    actual_length="$(wc -c < "${object_path}" | tr -d '[:space:]')"
    [[ "${actual_length}" == "${expected_length}" ]] ||
        fail "restored replay object length mismatch: ${key}"
    actual_etag="$(shasum -a 256 "${object_path}" | awk '{print $1}')"
    [[ "${actual_etag}" == "${expected_etag}" ]] ||
        fail "restored replay object checksum mismatch: ${key}"
    gzip -t "${object_path}" ||
        fail "restored replay object gzip validation failed: ${key}"
done < "${bundle_directory}/replay-objects.tsv"

actual_objects="$(find "${replay_root}" -type f -name '*.json.gz' |
    wc -l | tr -d '[:space:]')"
[[ "${actual_objects}" == "${expected_objects}" ]] ||
    fail "restored replay object count differs from bundle inventory"

echo "C4D1 restore verification: PASS"
