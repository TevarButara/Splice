BEGIN;

CREATE TABLE splice.raid_revenge_requests (
    id uuid PRIMARY KEY,
    source_raid_id uuid NOT NULL REFERENCES splice.raid_sessions(id),
    requester_player_id uuid NOT NULL REFERENCES splice.players(id),
    target_player_id uuid NOT NULL REFERENCES splice.players(id),
    target_deployment_id uuid NOT NULL REFERENCES splice.town_deployments(id),
    target_snapshot_id uuid NOT NULL REFERENCES splice.town_snapshots(id),
    state text NOT NULL,
    expires_at timestamptz NOT NULL,
    quoted_at timestamptz NULL,
    funded_at timestamptz NULL,
    started_at timestamptz NULL,
    cancelled_at timestamptz NULL,
    consumed_raid_id uuid NULL UNIQUE REFERENCES splice.raid_sessions(id),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT raid_revenge_not_self CHECK (requester_player_id <> target_player_id),
    CONSTRAINT raid_revenge_state_valid
        CHECK (state IN ('PREPARED', 'QUOTED', 'FUNDED', 'STARTED', 'CANCELLED'))
);

CREATE UNIQUE INDEX one_live_revenge_request_per_source_idx
    ON splice.raid_revenge_requests(source_raid_id, requester_player_id)
    WHERE state IN ('PREPARED', 'QUOTED', 'FUNDED');

CREATE INDEX raid_revenge_cooldown_idx
    ON splice.raid_revenge_requests(requester_player_id, source_raid_id, started_at DESC)
    WHERE state = 'STARTED';

ALTER TABLE splice.raid_quotes
    ADD COLUMN revenge_request_id uuid NULL UNIQUE
        REFERENCES splice.raid_revenge_requests(id);

CREATE INDEX raid_history_defender_completed_idx
    ON splice.raid_sessions(defender_player_id, completed_at DESC, id DESC)
    WHERE state = 'SETTLED';

CREATE INDEX raid_history_attacker_completed_idx
    ON splice.raid_sessions(attacker_player_id, completed_at DESC, id DESC)
    WHERE state = 'SETTLED';

COMMIT;
