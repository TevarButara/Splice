BEGIN;

ALTER TABLE splice.content_definitions
    ADD COLUMN raid_power integer NOT NULL DEFAULT 0 CHECK (raid_power >= 0);

CREATE TABLE splice.attacker_loadouts (
    id uuid PRIMARY KEY,
    owner_player_id uuid NOT NULL REFERENCES splice.players(id),
    faction_id text NOT NULL,
    revision bigint NOT NULL CHECK (revision > 0),
    hero_id text NOT NULL DEFAULT '',
    entries jsonb NOT NULL,
    payload_sha256 char(64) NOT NULL,
    raid_power bigint NOT NULL CHECK (raid_power > 0),
    content_version text NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT attacker_loadout_owner_unique UNIQUE (id, owner_player_id),
    CONSTRAINT attacker_loadout_hash_format CHECK (payload_sha256 ~ '^[0-9a-f]{64}$')
);

CREATE INDEX attacker_loadouts_owner_idx
    ON splice.attacker_loadouts(owner_player_id, updated_at DESC);

CREATE TABLE splice.attacker_loadout_snapshots (
    id uuid PRIMARY KEY,
    loadout_id uuid NOT NULL,
    owner_player_id uuid NOT NULL,
    faction_id text NOT NULL,
    revision bigint NOT NULL CHECK (revision > 0),
    hero_id text NOT NULL DEFAULT '',
    entries jsonb NOT NULL,
    payload_sha256 char(64) NOT NULL,
    raid_power bigint NOT NULL CHECK (raid_power > 0),
    content_version text NOT NULL,
    validator_version text NOT NULL,
    committed_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT attacker_loadout_snapshot_owner_fk
        FOREIGN KEY (loadout_id, owner_player_id)
        REFERENCES splice.attacker_loadouts(id, owner_player_id),
    CONSTRAINT attacker_loadout_snapshot_hash_format CHECK (payload_sha256 ~ '^[0-9a-f]{64}$')
);

CREATE INDEX attacker_loadout_snapshots_owner_idx
    ON splice.attacker_loadout_snapshots(owner_player_id, committed_at DESC);

ALTER TABLE splice.raid_quotes
    ADD COLUMN attacker_loadout_snapshot_id uuid NULL
        REFERENCES splice.attacker_loadout_snapshots(id);

ALTER TABLE splice.raid_allocations
    ADD COLUMN worker_id text NULL,
    ADD COLUMN lease_expires_at timestamptz NULL,
    ADD COLUMN heartbeat_at timestamptz NULL,
    ADD COLUMN claim_attempt integer NOT NULL DEFAULT 0 CHECK (claim_attempt >= 0),
    ADD CONSTRAINT raid_allocation_worker_not_blank
        CHECK (worker_id IS NULL OR btrim(worker_id) <> '');

CREATE INDEX raid_allocations_claim_idx
    ON splice.raid_allocations(raid_server_id, state, created_at);

CREATE OR REPLACE FUNCTION splice.reject_immutable_loadout_snapshot_change()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'IMMUTABLE_LOADOUT_SNAPSHOT: attacker loadout snapshots cannot be updated or deleted';
END;
$$;

CREATE TRIGGER attacker_loadout_snapshots_immutable
BEFORE UPDATE OR DELETE ON splice.attacker_loadout_snapshots
FOR EACH ROW EXECUTE FUNCTION splice.reject_immutable_loadout_snapshot_change();

COMMIT;
