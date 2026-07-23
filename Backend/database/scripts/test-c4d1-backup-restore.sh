#!/usr/bin/env bash
set -euo pipefail
export LC_ALL=C
export LANG=C
umask 077

source_database="splice_c4d1_backup_source_test"
target_database="splice_c4d1_backup_restore_test"
corrupt_database="splice_c4d1_backup_corrupt_test"
missing_database="splice_c4d1_backup_missing_test"
unsafe_database="splice_c4d1_backup_unsafe_test"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
backend_dir="$(cd "${script_dir}/../.." && pwd)"
database_dir="${backend_dir}/database"
database_user="$(id -un)"
proof_root="$(mktemp -d "${TMPDIR:-/tmp}/splice-c4d1-drill.XXXXXX")"
source_replay_root="${proof_root}/source-replays"
target_replay_root="${proof_root}/restored-replays"
bundle_directory="${proof_root}/bundle"
corrupt_bundle="${proof_root}/corrupt-bundle"
missing_bundle="${proof_root}/missing-bundle"
unsafe_bundle="${proof_root}/unsafe-bundle"
api_log="${proof_root}/api.log"
api_pid=""
server_port="${SPLICE_BACKUP_DRILL_PORT:-5093}"

directory_mode() {
    if stat -f '%Lp' "$1" >/dev/null 2>&1; then
        stat -f '%Lp' "$1"
    else
        stat -c '%a' "$1"
    fi
}

cleanup() {
    if [[ -n "${api_pid}" ]]; then
        kill "${api_pid}" >/dev/null 2>&1 || true
        wait "${api_pid}" >/dev/null 2>&1 || true
    fi
    dropdb --if-exists --force "${source_database}" >/dev/null 2>&1 || true
    dropdb --if-exists --force "${target_database}" >/dev/null 2>&1 || true
    dropdb --if-exists --force "${corrupt_database}" >/dev/null 2>&1 || true
    dropdb --if-exists --force "${missing_database}" >/dev/null 2>&1 || true
    dropdb --if-exists --force "${unsafe_database}" >/dev/null 2>&1 || true
    rm -rf "${proof_root}"
}
trap cleanup EXIT INT TERM

for database in "${source_database}" "${target_database}" \
    "${corrupt_database}" "${missing_database}" "${unsafe_database}"; do
    dropdb --if-exists --force "${database}" >/dev/null 2>&1
done
createdb "${source_database}"

for migration in "${database_dir}"/migrations/*.sql; do
    psql -X -v ON_ERROR_STOP=1 -d "${source_database}" \
        -f "${migration}" >/dev/null
done
for seed in "${database_dir}"/seeds/*.sql; do
    psql -X -v ON_ERROR_STOP=1 -d "${source_database}" \
        -f "${seed}" >/dev/null
done

storage_key="replays/v1/c4/c4d1-proof.json.gz"
source_object="${source_replay_root}/${storage_key}"
mkdir -p "$(dirname "${source_object}")"
commands='[{"tick":10,"type":"COMPLETE","actor":"simulation","target":"FULL_VICTORY","value":3}]'
printf "%s" "${commands}" | gzip -n >"${source_object}"
storage_etag="$(shasum -a 256 "${source_object}" | awk '{print $1}')"
storage_length="$(wc -c < "${source_object}" | tr -d '[:space:]')"
command_hash="$(printf "10|COMPLETE|simulation|FULL_VICTORY|3" |
    shasum -a 256 | awk '{print $1}')"

psql -X -v ON_ERROR_STOP=1 -d "${source_database}" \
    -v storage_key="${storage_key}" \
    -v storage_etag="${storage_etag}" \
    -v storage_length="${storage_length}" \
    -v command_hash="${command_hash}" \
    -f "${database_dir}/tests/031_c4d1_backup_fixture.sql" >/dev/null

backup_started="${SECONDS}"
bash "${script_dir}/backup-c4d1.sh" \
    "${source_database}" "${source_replay_root}" "${bundle_directory}"
backup_seconds=$((SECONDS - backup_started))
snapshot_connections="$(psql -X -qAt -d "${source_database}" -c \
    "SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND pid<>pg_backend_pid()")"
[[ "${snapshot_connections}" == "0" ]] || {
    echo "TEST_FAILED: exported snapshot connection leaked after backup" >&2
    exit 1
}
[[ "$(directory_mode "${bundle_directory}")" == "700" ]] || {
    echo "TEST_FAILED: backup bundle permissions are not private" >&2
    exit 1
}
restore_started="${SECONDS}"
bash "${script_dir}/restore-c4d1.sh" \
    "${bundle_directory}" "${target_database}" "${target_replay_root}"
restore_seconds=$((SECONDS - restore_started))

dotnet build "${backend_dir}/src/Splice.Backend.Api/Splice.Backend.Api.csproj" \
    --nologo >/dev/null
connection_string="Host=127.0.0.1;Port=5432;Database=${target_database};Username=${database_user};Pooling=false"
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__Splice="${connection_string}" \
ReplayStorage__LocalRoot="${target_replay_root}" \
ReplayStorage__MaintenanceEnabled=false \
RaidServer__DevelopmentKey="c4d1-drill-only" \
dotnet run --project "${backend_dir}/src/Splice.Backend.Api/Splice.Backend.Api.csproj" \
    --no-build --no-launch-profile --urls="http://127.0.0.1:${server_port}" \
    >"${api_log}" 2>&1 &
api_pid=$!

ready=false
for _ in {1..80}; do
    if curl --fail --silent "http://127.0.0.1:${server_port}/health" >/dev/null; then
        ready=true
        break
    fi
    if ! kill -0 "${api_pid}" >/dev/null 2>&1; then
        cat "${api_log}" >&2
        exit 1
    fi
    sleep 0.25
done
[[ "${ready}" == true ]] || {
    cat "${api_log}" >&2
    exit 1
}

replay_response="${proof_root}/replay-response.json"
curl --fail --silent \
    -H "Authorization: Bearer dev:11000000-0000-0000-0000-000000000021" \
    -H "X-Request-Id: 74000000-0000-0000-0000-000000000021" \
    "http://127.0.0.1:${server_port}/v1/raids/71000000-0000-0000-0000-000000000021/replay" \
    >"${replay_response}"
grep -F '"commandCount":1' "${replay_response}" >/dev/null
grep -F '"target":"FULL_VICTORY"' "${replay_response}" >/dev/null

kill "${api_pid}" >/dev/null 2>&1 || true
wait "${api_pid}" >/dev/null 2>&1 || true
api_pid=""

cp -R "${bundle_directory}" "${corrupt_bundle}"
IFS=$'\t' read -r first_key _ <"${corrupt_bundle}/replay-objects.tsv"
printf "corrupt" >>"${corrupt_bundle}/replay-objects/${first_key}"
if bash "${script_dir}/restore-c4d1.sh" \
    "${corrupt_bundle}" "${corrupt_database}" \
    "${proof_root}/corrupt-restore" >/dev/null 2>&1; then
    echo "TEST_FAILED: corrupted replay bundle restored successfully" >&2
    exit 1
fi
if psql -X -qAt -d postgres -v database="${corrupt_database}" <<'SQL' | grep -q 1
SELECT 1 FROM pg_database WHERE datname=:'database';
SQL
then
    echo "TEST_FAILED: corrupt preflight created a database" >&2
    exit 1
fi

cp -R "${bundle_directory}" "${missing_bundle}"
rm "${missing_bundle}/replay-objects/${first_key}"
if bash "${script_dir}/restore-c4d1.sh" \
    "${missing_bundle}" "${missing_database}" \
    "${proof_root}/missing-restore" >/dev/null 2>&1; then
    echo "TEST_FAILED: missing replay bundle restored successfully" >&2
    exit 1
fi
if psql -X -qAt -d postgres -v database="${missing_database}" <<'SQL' | grep -q 1
SELECT 1 FROM pg_database WHERE datname=:'database';
SQL
then
    echo "TEST_FAILED: missing-object preflight created a database" >&2
    exit 1
fi

cp -R "${bundle_directory}" "${unsafe_bundle}"
tab=$'\t'
sed "1s|^[^${tab}]*|../escape.json.gz|" \
    "${unsafe_bundle}/replay-objects.tsv" \
    >"${unsafe_bundle}/replay-objects.tsv.changed"
mv "${unsafe_bundle}/replay-objects.tsv.changed" \
    "${unsafe_bundle}/replay-objects.tsv"
if bash "${script_dir}/restore-c4d1.sh" \
    "${unsafe_bundle}" "${unsafe_database}" \
    "${proof_root}/unsafe-restore" >/dev/null 2>&1; then
    echo "TEST_FAILED: unsafe replay object key restored successfully" >&2
    exit 1
fi
if psql -X -qAt -d postgres -v database="${unsafe_database}" <<'SQL' | grep -q 1
SELECT 1 FROM pg_database WHERE datname=:'database';
SQL
then
    echo "TEST_FAILED: unsafe-key preflight created a database" >&2
    exit 1
fi

echo "C4D1B backup/restore drill: PASS (snapshot, fingerprint, ledger, replay, corrupt/missing/unsafe fail-closed)"
echo "C4D1B drill timing: backup=${backup_seconds}s restore+verify=${restore_seconds}s snapshot=$(cat "${bundle_directory}/snapshot-utc")"
