BEGIN;

CREATE TABLE splice.content_definitions (
    content_id text PRIMARY KEY,
    faction_id text NOT NULL,
    content_kind text NOT NULL,
    defense_capacity_cost integer NOT NULL DEFAULT 0 CHECK (defense_capacity_cost >= 0),
    gold_cost bigint NOT NULL DEFAULT 0 CHECK (gold_cost >= 0),
    enabled boolean NOT NULL DEFAULT true,
    content_version text NOT NULL,
    CONSTRAINT content_kind_valid CHECK (content_kind IN ('TOWER', 'GARRISON', 'MINER'))
);

CREATE INDEX content_definitions_faction_idx
    ON splice.content_definitions(faction_id, content_kind) WHERE enabled;

ALTER TABLE splice.towns
    ADD COLUMN updated_at timestamptz NOT NULL DEFAULT clock_timestamp();

CREATE TABLE splice.town_drafts (
    town_id uuid PRIMARY KEY REFERENCES splice.towns(id),
    version bigint NOT NULL CHECK (version > 0),
    payload jsonb NOT NULL,
    payload_hash char(64) NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT town_draft_hash_format CHECK (payload_hash ~ '^[0-9a-f]{64}$')
);

CREATE TABLE splice.town_layout_commits (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    town_id uuid NOT NULL REFERENCES splice.towns(id),
    revision integer NOT NULL CHECK (revision > 0),
    draft_version bigint NOT NULL CHECK (draft_version > 0),
    payload jsonb NOT NULL,
    payload_hash char(64) NOT NULL,
    build_value bigint NOT NULL CHECK (build_value >= 0),
    checkout_cost bigint NOT NULL,
    checkout_transaction_id uuid NULL REFERENCES splice.ledger_transactions(id),
    content_version text NOT NULL,
    validator_version text NOT NULL,
    committed_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT town_layout_commit_revision_unique UNIQUE (town_id, revision),
    CONSTRAINT town_layout_commit_hash_format CHECK (payload_hash ~ '^[0-9a-f]{64}$')
);

CREATE TABLE splice.town_vaults (
    town_id uuid NOT NULL REFERENCES splice.towns(id),
    currency_code text NOT NULL REFERENCES splice.currencies(code),
    ledger_account_id uuid NOT NULL UNIQUE REFERENCES splice.ledger_accounts(id),
    lootable_cap bigint NOT NULL DEFAULT 0 CHECK (lootable_cap >= 0),
    version bigint NOT NULL DEFAULT 0,
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    PRIMARY KEY (town_id, currency_code)
);

CREATE TABLE splice.town_escrows (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    town_id uuid NOT NULL REFERENCES splice.towns(id),
    ledger_account_id uuid NOT NULL UNIQUE REFERENCES splice.ledger_accounts(id),
    currency_code text NOT NULL REFERENCES splice.currencies(code),
    funded_amount bigint NOT NULL CHECK (funded_amount > 0),
    state text NOT NULL,
    funded_transaction_id uuid NOT NULL REFERENCES splice.ledger_transactions(id),
    refunded_transaction_id uuid NULL REFERENCES splice.ledger_transactions(id),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    refunded_at timestamptz NULL,
    CONSTRAINT town_escrow_state_valid CHECK (state IN ('FUNDED', 'ACTIVE', 'RETIRING', 'REFUNDED'))
);

CREATE UNIQUE INDEX one_open_town_escrow_idx
    ON splice.town_escrows(town_id, currency_code)
    WHERE state IN ('FUNDED', 'ACTIVE', 'RETIRING');

ALTER TABLE splice.town_snapshots
    ADD COLUMN layout_commit_id uuid NULL REFERENCES splice.town_layout_commits(id),
    ADD COLUMN used_capacity integer NULL CHECK (used_capacity >= 0),
    ADD COLUMN max_capacity integer NULL CHECK (max_capacity >= 0),
    ADD COLUMN tower_count integer NULL CHECK (tower_count >= 0),
    ADD COLUMN garrison_count integer NULL CHECK (garrison_count >= 0),
    ADD COLUMN matchmaking_eligible boolean NOT NULL DEFAULT true,
    ADD COLUMN validation_warnings jsonb NOT NULL DEFAULT '[]'::jsonb;

ALTER TABLE splice.town_deployments
    ADD COLUMN town_escrow_id uuid NULL REFERENCES splice.town_escrows(id);

CREATE OR REPLACE FUNCTION splice.reject_immutable_town_record_change()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'IMMUTABLE_TOWN_RECORD: % rows cannot be updated or deleted', TG_TABLE_NAME;
END;
$$;

CREATE TRIGGER town_layout_commits_immutable
BEFORE UPDATE OR DELETE ON splice.town_layout_commits
FOR EACH ROW EXECUTE FUNCTION splice.reject_immutable_town_record_change();

CREATE TRIGGER town_snapshots_immutable
BEFORE UPDATE OR DELETE ON splice.town_snapshots
FOR EACH ROW EXECUTE FUNCTION splice.reject_immutable_town_record_change();

COMMIT;
