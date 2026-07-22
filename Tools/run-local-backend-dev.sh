#!/usr/bin/env bash
set -euo pipefail

uuid_pattern='^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'
if [[ $# -ne 1 || ! "$1" =~ $uuid_pattern ]]; then
    echo "Usage: bash Tools/run-local-backend-dev.sh <unity-player-uuid>" >&2
    exit 2
fi

player_id="$1"
database_name="splice_unity_local_dev"
server_port="${SPLICE_BACKEND_PORT:-5080}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_dir="$(cd "${script_dir}/.." && pwd)"
backend_dir="${repository_dir}/Backend"
database_user="$(id -un)"
api_pid=""

cleanup() {
    if [[ -n "${api_pid}" ]]; then kill "${api_pid}" >/dev/null 2>&1 || true; fi
    dropdb --if-exists "${database_name}" >/dev/null 2>&1 || true
}
trap cleanup EXIT INT TERM

dropdb --if-exists "${database_name}" >/dev/null 2>&1
createdb "${database_name}"
for migration in "${backend_dir}"/database/migrations/*.sql; do
    psql -X -v ON_ERROR_STOP=1 -d "${database_name}" -f "${migration}" >/dev/null
done
for seed in "${backend_dir}"/database/seeds/*.sql; do
    psql -X -v ON_ERROR_STOP=1 -d "${database_name}" -f "${seed}" >/dev/null
done

psql -X -v ON_ERROR_STOP=1 -d "${database_name}" -v player_id="${player_id}" <<'SQL' >/dev/null
SET search_path = splice, public;
INSERT INTO players (id, display_name) VALUES
  (:'player_id'::uuid, 'Unity Local Player'),
  ('11000000-0000-0000-0000-000000000002', 'Defender Alpha');

INSERT INTO ledger_accounts (id, account_key, owner_type, owner_id, currency_code) VALUES
  ('21000000-0000-0000-0000-000000000001','local-dev:player:war-gem','PLAYER',:'player_id'::uuid,'WAR_GEM'),
  ('21000000-0000-0000-0000-000000000002','local-dev:player:gold','PLAYER',:'player_id'::uuid,'GOLD');

SELECT post_ledger_transaction(
  'local-dev:mint:war-gem', 'TEST_MINT', 'TEST', NULL,
  jsonb_build_array(
    jsonb_build_object('account_id','00000000-0000-0000-0000-000000000201','amount',-1000),
    jsonb_build_object('account_id','21000000-0000-0000-0000-000000000001','amount',1000)));
SELECT post_ledger_transaction(
  'local-dev:mint:gold', 'TEST_MINT', 'TEST', NULL,
  jsonb_build_array(
    jsonb_build_object('account_id','00000000-0000-0000-0000-000000000101','amount',-5000),
    jsonb_build_object('account_id','21000000-0000-0000-0000-000000000002','amount',5000)));

INSERT INTO towns (id, owner_player_id, faction_id, base_level) VALUES
  ('31000000-0000-0000-0000-000000000001','11000000-0000-0000-0000-000000000002','1',1);
INSERT INTO ledger_accounts (id, account_key, owner_type, owner_id, currency_code) VALUES
  ('21000000-0000-0000-0000-000000000003','local-dev:defender-town:war-gem','TOWN',
   '31000000-0000-0000-0000-000000000001','WAR_GEM');
SELECT post_ledger_transaction(
  'local-dev:mint:defender-town-war-gem', 'TEST_MINT', 'TEST', NULL,
  jsonb_build_array(
    jsonb_build_object('account_id','00000000-0000-0000-0000-000000000201','amount',-100),
    jsonb_build_object('account_id','21000000-0000-0000-0000-000000000003','amount',100)));
INSERT INTO town_escrows
  (id, town_id, ledger_account_id, currency_code, funded_amount, state, funded_transaction_id) VALUES
  ('42000000-0000-0000-0000-000000000001','31000000-0000-0000-0000-000000000001',
   '21000000-0000-0000-0000-000000000003','WAR_GEM',100,'ACTIVE',
   (SELECT id FROM ledger_transactions WHERE idempotency_key='local-dev:mint:defender-town-war-gem'));
INSERT INTO town_snapshots
  (id, town_id, revision, payload, payload_sha256, faction_id, base_level, base_power,
   content_version, validator_version, used_capacity, max_capacity, tower_count,
   garrison_count, matchmaking_eligible) VALUES
  ('32000000-0000-0000-0000-000000000001','31000000-0000-0000-0000-000000000001',1,
   '{"schemaVersion":1,"snapshotId":"32000000-0000-0000-0000-000000000001","deploymentId":"41000000-0000-0000-0000-000000000001","revision":1,"committedUtc":"2026-07-22T00:00:00Z","ownerAccountId":"11000000-0000-0000-0000-000000000002","factionId":"1","baseLevel":1,"basePowerRating":405,"usedCapacity":2,"maxCapacity":100,"matchmakingEligible":true,"validationVersion":"server-c3-v1","validationWarnings":[],"layout":{"version":1,"ownerAccountId":"11000000-0000-0000-0000-000000000002","factionId":"1","towers":[{"towerId":"1/1","position":{"x":0,"y":0,"z":0},"attackLevel":0,"healthLevel":0,"armorLevel":0,"rangeLevel":0,"targetsLevel":0}],"garrison":[],"minerCardIds":[],"storedGold":100},"armyShowcasePresetName":"","heroAppearanceId":""}',
   repeat('a',64),'1',1,405,'content-c3-v1','server-c3-v1',2,100,1,0,true);
INSERT INTO town_deployments (id, town_id, active_snapshot_id, town_escrow_id, status, stake_band) VALUES
  ('41000000-0000-0000-0000-000000000001','31000000-0000-0000-0000-000000000001',
   '32000000-0000-0000-0000-000000000001','42000000-0000-0000-0000-000000000001',
   'ACTIVE','FAIR');
SQL

connection_string="Host=127.0.0.1;Port=5432;Database=${database_name};Username=${database_user};Pooling=false"
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__Splice="${connection_string}" \
Reconciliation__Enabled=false \
RaidServer__DevelopmentKey="local-only-c4-trusted-key-2026" \
RaidServer__DefaultServerId="local-authoritative-raid-1" \
dotnet run --project "${backend_dir}/src/Splice.Backend.Api/Splice.Backend.Api.csproj" \
  --no-launch-profile --urls="http://127.0.0.1:${server_port}" &
api_pid=$!

for attempt in {1..40}; do
    if curl --fail --silent "http://127.0.0.1:${server_port}/health" >/dev/null; then break; fi
    if ! kill -0 "${api_pid}" >/dev/null 2>&1; then
        echo "Local Splice API stopped before becoming ready." >&2
        exit 1
    fi
    sleep 0.25
done
curl --fail --silent "http://127.0.0.1:${server_port}/health" >/dev/null
echo "Splice local backend READY at http://127.0.0.1:${server_port}"
echo "Unity player: ${player_id}"
echo "Keep this terminal open; Ctrl+C stops the API and removes the temporary database."
wait "${api_pid}"
