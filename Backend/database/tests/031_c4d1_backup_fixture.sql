\set ON_ERROR_STOP on

BEGIN;

INSERT INTO splice.players (id, display_name) VALUES
  ('11000000-0000-0000-0000-000000000021', 'Backup Attacker'),
  ('11000000-0000-0000-0000-000000000022', 'Backup Defender');

INSERT INTO splice.ledger_accounts
  (id, account_key, owner_type, owner_id, currency_code)
VALUES
  ('21000000-0000-0000-0000-000000000021',
   'test:c4d1:attacker:war-gem', 'PLAYER',
   '11000000-0000-0000-0000-000000000021', 'WAR_GEM');

SELECT splice.post_ledger_transaction(
  'test:c4d1:mint', 'TEST_MINT', 'TEST', NULL,
  jsonb_build_array(
    jsonb_build_object(
      'account_id', '00000000-0000-0000-0000-000000000201',
      'amount', -250),
    jsonb_build_object(
      'account_id', '21000000-0000-0000-0000-000000000021',
      'amount', 250)));

INSERT INTO splice.towns (id, owner_player_id, faction_id)
VALUES ('31000000-0000-0000-0000-000000000021',
        '11000000-0000-0000-0000-000000000022', 'backup-proof');

INSERT INTO splice.town_snapshots
  (id, town_id, revision, payload, payload_sha256, faction_id,
   base_level, base_power, content_version, validator_version)
VALUES
  ('32000000-0000-0000-0000-000000000021',
   '31000000-0000-0000-0000-000000000021', 1,
   '{"layout":{},"defenseUnits":[]}'::jsonb,
   encode(public.digest(
     '{"layout":{},"defenseUnits":[]}'::jsonb::text, 'sha256'), 'hex'),
   'backup-proof', 1, 100, 'backup-proof-v1', 'backup-proof-v1');

INSERT INTO splice.town_deployments
  (id, town_id, active_snapshot_id, status, stake_band)
VALUES
  ('41000000-0000-0000-0000-000000000021',
   '31000000-0000-0000-0000-000000000021',
   '32000000-0000-0000-0000-000000000021', 'ACTIVE', 'FAIR');

INSERT INTO splice.attacker_loadouts
  (id, owner_player_id, faction_id, revision, hero_id, entries,
   payload_sha256, army_power, hero_power, gear_power,
   hero_payload, gear_items, army_items, content_version)
VALUES
  ('51000000-0000-0000-0000-000000000021',
   '11000000-0000-0000-0000-000000000021',
   'backup-proof', 1, '', '[]'::jsonb, repeat('a', 64),
   1, 0, 0, '{}'::jsonb, '[]'::jsonb, '[]'::jsonb, 'backup-proof-v1');

INSERT INTO splice.attacker_loadout_snapshots
  (id, loadout_id, owner_player_id, faction_id, revision, hero_id,
   entries, payload_sha256, army_power, hero_power, gear_power,
   hero_payload, gear_items, army_items, content_version, validator_version)
VALUES
  ('52000000-0000-0000-0000-000000000021',
   '51000000-0000-0000-0000-000000000021',
   '11000000-0000-0000-0000-000000000021',
   'backup-proof', 1, '', '[]'::jsonb, repeat('a', 64),
   1, 0, 0, '{}'::jsonb, '[]'::jsonb, '[]'::jsonb,
   'backup-proof-v1', 'backup-proof-v1');

INSERT INTO splice.raid_quotes
  (id, attacker_player_id, target_deployment_id, target_snapshot_id,
   attacker_loadout_id, attacker_loadout_snapshot_id, difficulty_band,
   attacker_stake, defender_max_loss, full_victory_payout,
   outer_payout, inner_payout, core_payout, rules_version, expires_at)
VALUES
  ('61000000-0000-0000-0000-000000000021',
   '11000000-0000-0000-0000-000000000021',
   '41000000-0000-0000-0000-000000000021',
   '32000000-0000-0000-0000-000000000021',
   '51000000-0000-0000-0000-000000000021',
   '52000000-0000-0000-0000-000000000021',
   'FAIR', 100, 100, 180, 20, 40, 120,
   'backup-proof-v1', clock_timestamp() + interval '1 hour');

INSERT INTO splice.raid_sessions
  (id, quote_id, attacker_player_id, defender_player_id,
   target_snapshot_id, state, scene_contract_version,
   raid_server_id, started_at, completed_at)
VALUES
  ('71000000-0000-0000-0000-000000000021',
   '61000000-0000-0000-0000-000000000021',
   '11000000-0000-0000-0000-000000000021',
   '11000000-0000-0000-0000-000000000022',
   '32000000-0000-0000-0000-000000000021',
   'SETTLED', 'raid-scene-c4d1-v1', 'backup-proof-server',
   clock_timestamp(), clock_timestamp());

INSERT INTO splice.raid_allocations
  (id, raid_id, raid_server_id, ticket_hash, state,
   expires_at, claimed_at, completed_at)
VALUES
  ('72000000-0000-0000-0000-000000000021',
   '71000000-0000-0000-0000-000000000021',
   'backup-proof-server', repeat('b', 64), 'COMPLETED',
   clock_timestamp() + interval '1 hour',
   clock_timestamp(), clock_timestamp());

INSERT INTO splice.raid_results
  (id, raid_id, allocation_id, raid_server_id, outcome,
   breached_rings, duration_ms, simulation_hash,
   war_gem_payout, result_payload)
VALUES
  ('73000000-0000-0000-0000-000000000021',
   '71000000-0000-0000-0000-000000000021',
   '72000000-0000-0000-0000-000000000021',
   'backup-proof-server', 'FULL_VICTORY', 3, 1000,
   repeat('c', 64), 180, '{"proof":"c4d1"}'::jsonb);

UPDATE splice.raid_sessions
   SET result_id = '73000000-0000-0000-0000-000000000021'
 WHERE id = '71000000-0000-0000-0000-000000000021';

INSERT INTO splice.raid_replays
  (result_id, raid_id, simulation_version, tick_count, command_count,
   command_stream_hash, command_stream, storage_provider, storage_key,
   storage_etag, storage_content_length, storage_encoding)
VALUES
  ('73000000-0000-0000-0000-000000000021',
   '71000000-0000-0000-0000-000000000021',
   'fixed-tick-c4c2c-v2', 10, 1, :'command_hash', NULL,
   'local-filesystem', :'storage_key', :'storage_etag',
   :storage_length, 'gzip');

COMMIT;
