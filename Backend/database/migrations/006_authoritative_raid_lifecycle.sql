BEGIN;

ALTER TABLE splice.raid_escrows
    ADD COLUMN defender_town_escrow_id uuid NULL REFERENCES splice.town_escrows(id),
    ADD COLUMN defender_reserved_amount bigint NOT NULL DEFAULT 0
        CHECK (defender_reserved_amount >= 0);

CREATE TABLE splice.raid_allocations (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    raid_id uuid NOT NULL UNIQUE REFERENCES splice.raid_sessions(id),
    raid_server_id text NOT NULL,
    ticket_hash char(64) NOT NULL UNIQUE,
    state text NOT NULL,
    expires_at timestamptz NOT NULL,
    claimed_at timestamptz NULL,
    completed_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT raid_allocation_server_not_blank CHECK (btrim(raid_server_id) <> ''),
    CONSTRAINT raid_allocation_ticket_hash_format CHECK (ticket_hash ~ '^[0-9a-f]{64}$'),
    CONSTRAINT raid_allocation_state_valid
        CHECK (state IN ('ALLOCATED', 'CLAIMED', 'COMPLETED', 'EXPIRED'))
);

CREATE INDEX raid_allocations_expiry_idx
    ON splice.raid_allocations(state, expires_at);

CREATE TABLE splice.raid_results (
    id uuid PRIMARY KEY,
    raid_id uuid NOT NULL UNIQUE REFERENCES splice.raid_sessions(id),
    allocation_id uuid NOT NULL UNIQUE REFERENCES splice.raid_allocations(id),
    raid_server_id text NOT NULL,
    outcome text NOT NULL,
    breached_rings integer NOT NULL CHECK (breached_rings BETWEEN 0 AND 3),
    duration_ms integer NOT NULL CHECK (duration_ms BETWEEN 1000 AND 3600000),
    simulation_hash char(64) NOT NULL,
    war_gem_payout bigint NOT NULL CHECK (war_gem_payout >= 0),
    result_payload jsonb NOT NULL,
    received_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT raid_result_outcome_valid CHECK (outcome IN ('FULL_VICTORY', 'EXTRACTED', 'DEFEAT')),
    CONSTRAINT raid_result_simulation_hash_format CHECK (simulation_hash ~ '^[0-9a-f]{64}$')
);

ALTER TABLE splice.raid_sessions
    ADD COLUMN result_id uuid NULL UNIQUE REFERENCES splice.raid_results(id);

CREATE OR REPLACE FUNCTION splice.reject_immutable_raid_result_change()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'IMMUTABLE_RAID_RESULT: raid result rows cannot be updated or deleted';
END;
$$;

CREATE TRIGGER raid_results_immutable
BEFORE UPDATE OR DELETE ON splice.raid_results
FOR EACH ROW EXECUTE FUNCTION splice.reject_immutable_raid_result_change();

COMMIT;
