#!/usr/bin/env bash
set -euo pipefail

database_name="splice_c4c2e_load_test"
server_port="${SPLICE_LOAD_PORT:-5082}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
backend_dir="$(cd "${script_dir}/../.." && pwd)"
database_dir="${backend_dir}/database"
database_user="$(id -un)"
api_pid=""
api_log="$(mktemp -t splice-c4c2e-api.XXXXXX)"

cleanup() {
    if [[ -n "${api_pid}" ]]; then kill "${api_pid}" >/dev/null 2>&1 || true; fi
    dropdb --if-exists "${database_name}" >/dev/null 2>&1 || true
    rm -f "${api_log}"
}
trap cleanup EXIT INT TERM

dropdb --if-exists "${database_name}" >/dev/null 2>&1
createdb "${database_name}"
for migration in "${database_dir}"/migrations/*.sql; do
    psql -X -v ON_ERROR_STOP=1 -d "${database_name}" -f "${migration}" >/dev/null
done
for seed in "${database_dir}"/seeds/*.sql; do
    psql -X -v ON_ERROR_STOP=1 -d "${database_name}" -f "${seed}" >/dev/null
done
psql -X -v ON_ERROR_STOP=1 -d "${database_name}" \
    -f "${database_dir}/tests/030_c4c2e_load_fixture.sql" >/dev/null

connection_string="Host=127.0.0.1;Port=5432;Database=${database_name};Username=${database_user};Pooling=true;Maximum Pool Size=100"
dotnet build "${backend_dir}/src/Splice.Backend.Api/Splice.Backend.Api.csproj" --nologo >/dev/null
dotnet build "${backend_dir}/tools/Splice.Backend.LoadTests/Splice.Backend.LoadTests.csproj" --nologo >/dev/null

ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__Splice="${connection_string}" \
Reconciliation__Enabled=false \
RaidServer__DevelopmentKey="local-c4c2e-load-key-2026" \
RaidServer__DefaultServerId="local-c4c2e-load-server" \
dotnet run --project "${backend_dir}/src/Splice.Backend.Api/Splice.Backend.Api.csproj" \
    --no-build --no-launch-profile --urls="http://127.0.0.1:${server_port}" >"${api_log}" 2>&1 &
api_pid=$!

ready=false
for _ in {1..60}; do
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
if [[ "${ready}" != true ]]; then
    cat "${api_log}" >&2
    exit 1
fi

set +e
SPLICE_LOAD_BASE_URL="http://127.0.0.1:${server_port}" \
SPLICE_LOAD_PLAYER_ID="11000000-0000-0000-0000-000000000011" \
SPLICE_LOAD_RAID_SERVER_ID="local-c4c2e-load-server" \
SPLICE_LOAD_RAID_SERVER_KEY="local-c4c2e-load-key-2026" \
SPLICE_LOAD_REQUESTS="${SPLICE_LOAD_REQUESTS:-240}" \
SPLICE_LOAD_CONCURRENCY="${SPLICE_LOAD_CONCURRENCY:-16}" \
SPLICE_LOAD_MIN_RPS="${SPLICE_LOAD_MIN_RPS:-20}" \
dotnet run --project "${backend_dir}/tools/Splice.Backend.LoadTests/Splice.Backend.LoadTests.csproj" \
    --no-build --no-launch-profile
load_status=$?
set -e
if [[ ${load_status} -ne 0 ]]; then
    echo "C4C2E API log after failed load budget:" >&2
    cat "${api_log}" >&2
fi
exit "${load_status}"
