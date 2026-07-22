#!/usr/bin/env bash
set -euo pipefail

database_name="splice_c1_test"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
database_dir="$(cd "${script_dir}/.." && pwd)"
temp_dir="$(mktemp -d)"

cleanup() {
    dropdb --if-exists "${database_name}" >/dev/null 2>&1 || true
    rm -rf "${temp_dir}"
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

psql -X -v ON_ERROR_STOP=1 -d "${database_name}" \
    -f "${database_dir}/tests/010_ledger_regression.sql" -A -t
psql -X -v ON_ERROR_STOP=1 -d "${database_name}" \
    -f "${database_dir}/tests/020_concurrency_setup.sql" >/dev/null

transfer_a="SELECT splice.post_ledger_transaction(
  'test:concurrency:transfer:a', 'TEST_CONCURRENT_TRANSFER', 'TEST', NULL,
  jsonb_build_array(
    jsonb_build_object('account_id','20000000-0000-0000-0000-000000000003','amount',-700),
    jsonb_build_object('account_id','20000000-0000-0000-0000-000000000004','amount',700)
  ));"
transfer_b="SELECT splice.post_ledger_transaction(
  'test:concurrency:transfer:b', 'TEST_CONCURRENT_TRANSFER', 'TEST', NULL,
  jsonb_build_array(
    jsonb_build_object('account_id','20000000-0000-0000-0000-000000000003','amount',-700),
    jsonb_build_object('account_id','20000000-0000-0000-0000-000000000005','amount',700)
  ));"

set +e
psql -X -v ON_ERROR_STOP=1 -d "${database_name}" -c "${transfer_a}" \
    >"${temp_dir}/transfer-a.log" 2>&1 &
pid_a=$!
psql -X -v ON_ERROR_STOP=1 -d "${database_name}" -c "${transfer_b}" \
    >"${temp_dir}/transfer-b.log" 2>&1 &
pid_b=$!
wait "${pid_a}"; status_a=$?
wait "${pid_b}"; status_b=$?
set -e

if [[ "${status_a}" -eq "${status_b}" ]]; then
    echo "ledger_concurrency: FAIL (expected exactly one debit to succeed)" >&2
    exit 1
fi

psql -X -v ON_ERROR_STOP=1 -d "${database_name}" \
    -f "${database_dir}/tests/021_concurrency_assert.sql" -A -t

echo "C1 database tests: PASS"
