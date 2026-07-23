BEGIN;

CREATE TABLE splice.raid_replays (
    result_id uuid PRIMARY KEY REFERENCES splice.raid_results(id),
    raid_id uuid NOT NULL UNIQUE REFERENCES splice.raid_sessions(id),
    simulation_version text NOT NULL,
    tick_count integer NOT NULL CHECK (tick_count BETWEEN 1 AND 36000),
    command_count integer NOT NULL CHECK (command_count BETWEEN 1 AND 25000),
    command_stream_hash char(64) NOT NULL,
    command_stream jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT raid_replay_version_not_blank CHECK (btrim(simulation_version) <> ''),
    CONSTRAINT raid_replay_command_hash_format
        CHECK (command_stream_hash ~ '^[0-9a-f]{64}$'),
    CONSTRAINT raid_replay_stream_array
        CHECK (jsonb_typeof(command_stream) = 'array'),
    CONSTRAINT raid_replay_stream_count
        CHECK (jsonb_array_length(command_stream) = command_count)
);

CREATE OR REPLACE FUNCTION splice.reject_immutable_raid_replay_change()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'IMMUTABLE_RAID_REPLAY: raid replay rows cannot be updated or deleted';
END;
$$;

CREATE TRIGGER raid_replays_immutable
BEFORE UPDATE OR DELETE ON splice.raid_replays
FOR EACH ROW EXECUTE FUNCTION splice.reject_immutable_raid_replay_change();

COMMIT;
