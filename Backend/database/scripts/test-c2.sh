#!/usr/bin/env bash
set -euo pipefail

database_name="splice_c2_test"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
backend_dir="$(cd "${script_dir}/../.." && pwd)"
database_dir="${backend_dir}/database"
database_user="$(id -un)"

cleanup() {
    dropdb --if-exists "${database_name}" >/dev/null 2>&1 || true
}
trap cleanup EXIT

dropdb --if-exists "${database_name}" >/dev/null 2>&1
createdb "${database_name}"

for migration in "${database_dir}"/migrations/*.sql; do
    psql -X -v ON_ERROR_STOP=1 -d "${database_name}" -f "${migration}" >/dev/null
done
for seed in "${database_dir}"/seeds/*.sql; do
    psql -X -v ON_ERROR_STOP=1 -d "${database_name}" -f "${seed}" >/dev/null
done

connection_string="Host=127.0.0.1;Port=5432;Database=${database_name};Username=${database_user};Pooling=false"
dotnet run --project "${backend_dir}/tests/Splice.Backend.Api.Tests/Splice.Backend.Api.Tests.csproj" \
    --no-launch-profile -- "${connection_string}"
