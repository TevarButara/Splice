BEGIN;

CREATE TABLE splice.towns (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    owner_player_id uuid NOT NULL REFERENCES splice.players(id),
    faction_id text NOT NULL,
    base_level integer NOT NULL DEFAULT 1 CHECK (base_level > 0),
    draft_version bigint NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT town_owner_faction_unique UNIQUE (owner_player_id, faction_id)
);

CREATE TABLE splice.town_snapshots (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    town_id uuid NOT NULL REFERENCES splice.towns(id),
    revision integer NOT NULL CHECK (revision > 0),
    payload jsonb NOT NULL,
    payload_sha256 char(64) NOT NULL,
    faction_id text NOT NULL,
    base_level integer NOT NULL CHECK (base_level > 0),
    base_power bigint NOT NULL CHECK (base_power >= 0),
    content_version text NOT NULL,
    validator_version text NOT NULL,
    committed_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT town_snapshot_revision_unique UNIQUE (town_id, revision),
    CONSTRAINT town_snapshot_hash_format CHECK (payload_sha256 ~ '^[0-9a-f]{64}$')
);

CREATE TABLE splice.town_deployments (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    town_id uuid NOT NULL REFERENCES splice.towns(id),
    active_snapshot_id uuid NOT NULL REFERENCES splice.town_snapshots(id),
    status text NOT NULL,
    stake_band text NOT NULL,
    shield_until timestamptz NULL,
    activated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    retired_at timestamptz NULL,
    CONSTRAINT town_deployment_status_valid CHECK (status IN ('READY', 'ACTIVE', 'PAUSED', 'SHIELDED', 'RETIRED')),
    CONSTRAINT town_deployment_stake_band_valid CHECK (stake_band IN ('FAIR', 'RISKY', 'HIGH'))
);

CREATE UNIQUE INDEX one_active_deployment_per_town_idx
    ON splice.town_deployments(town_id)
    WHERE status IN ('READY', 'ACTIVE', 'PAUSED', 'SHIELDED');

CREATE TABLE splice.raid_quotes (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    attacker_player_id uuid NOT NULL REFERENCES splice.players(id),
    target_deployment_id uuid NOT NULL REFERENCES splice.town_deployments(id),
    target_snapshot_id uuid NOT NULL REFERENCES splice.town_snapshots(id),
    attacker_loadout_id uuid NOT NULL,
    difficulty_band text NOT NULL,
    attacker_stake bigint NOT NULL CHECK (attacker_stake > 0),
    defender_max_loss bigint NOT NULL CHECK (defender_max_loss >= 0),
    full_victory_payout bigint NOT NULL CHECK (full_victory_payout >= 0),
    outer_payout bigint NOT NULL CHECK (outer_payout >= 0),
    inner_payout bigint NOT NULL CHECK (inner_payout >= 0),
    core_payout bigint NOT NULL CHECK (core_payout >= 0),
    rules_version text NOT NULL,
    expires_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE INDEX raid_quotes_attacker_expiry_idx
    ON splice.raid_quotes(attacker_player_id, expires_at DESC);

CREATE TABLE splice.raid_sessions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    quote_id uuid NOT NULL UNIQUE REFERENCES splice.raid_quotes(id),
    attacker_player_id uuid NOT NULL REFERENCES splice.players(id),
    defender_player_id uuid NOT NULL REFERENCES splice.players(id),
    target_snapshot_id uuid NOT NULL REFERENCES splice.town_snapshots(id),
    state text NOT NULL,
    scene_contract_version text NOT NULL,
    raid_server_id text NULL,
    started_at timestamptz NULL,
    completed_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT raid_session_state_valid CHECK (state IN ('PREPARED', 'FUNDED', 'ACTIVE', 'SETTLING', 'SETTLED', 'REFUNDED')),
    CONSTRAINT raid_session_not_self CHECK (attacker_player_id <> defender_player_id)
);

CREATE UNIQUE INDEX one_open_raid_per_attacker_idx
    ON splice.raid_sessions(attacker_player_id)
    WHERE state IN ('PREPARED', 'FUNDED', 'ACTIVE', 'SETTLING');

CREATE TABLE splice.raid_escrows (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    raid_id uuid NOT NULL UNIQUE REFERENCES splice.raid_sessions(id),
    ledger_account_id uuid NOT NULL UNIQUE REFERENCES splice.ledger_accounts(id),
    currency_code text NOT NULL REFERENCES splice.currencies(code),
    funded_amount bigint NOT NULL CHECK (funded_amount > 0),
    state text NOT NULL,
    funded_transaction_id uuid NOT NULL REFERENCES splice.ledger_transactions(id),
    settlement_transaction_id uuid NULL REFERENCES splice.ledger_transactions(id),
    refunded_transaction_id uuid NULL REFERENCES splice.ledger_transactions(id),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    settled_at timestamptz NULL,
    CONSTRAINT raid_escrow_state_valid CHECK (state IN ('FUNDED', 'ACTIVE', 'SETTLED', 'REFUNDED'))
);

COMMIT;
