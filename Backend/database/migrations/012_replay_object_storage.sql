BEGIN;

ALTER TABLE splice.raid_replays
    ADD COLUMN storage_provider text NULL,
    ADD COLUMN storage_key text NULL,
    ADD COLUMN storage_etag char(64) NULL,
    ADD COLUMN storage_content_length bigint NULL,
    ADD COLUMN storage_encoding text NULL;

-- Existing C4C2D-F rows remain readable without a destructive one-shot blob migration.
-- A later backfill worker may copy them to object storage and insert new immutable metadata,
-- but rollout never makes historical participant replays disappear.
ALTER TABLE splice.raid_replays DISABLE TRIGGER raid_replays_immutable;
UPDATE splice.raid_replays
   SET storage_provider = 'postgres-jsonb',
       storage_key = 'legacy-db:' || raid_id::text,
       storage_etag = command_stream_hash,
       storage_content_length = octet_length(command_stream::text),
       storage_encoding = 'identity';
ALTER TABLE splice.raid_replays ENABLE TRIGGER raid_replays_immutable;

ALTER TABLE splice.raid_replays
    ALTER COLUMN storage_provider SET NOT NULL,
    ALTER COLUMN storage_key SET NOT NULL,
    ALTER COLUMN storage_etag SET NOT NULL,
    ALTER COLUMN storage_content_length SET NOT NULL,
    ALTER COLUMN storage_encoding SET NOT NULL,
    ALTER COLUMN command_stream DROP NOT NULL,
    DROP CONSTRAINT raid_replay_stream_array,
    DROP CONSTRAINT raid_replay_stream_count,
    ADD CONSTRAINT raid_replay_storage_provider_valid
        CHECK (storage_provider IN ('postgres-jsonb', 'local-filesystem', 's3-compatible')),
    ADD CONSTRAINT raid_replay_storage_key_valid
        CHECK (length(storage_key) BETWEEN 1 AND 512
               AND storage_key !~ '(^/|[\\]|(^|/)[.][.](/|$))'),
    ADD CONSTRAINT raid_replay_storage_etag_format
        CHECK (storage_etag ~ '^[0-9a-f]{64}$'),
    ADD CONSTRAINT raid_replay_storage_length_valid
        CHECK (storage_content_length BETWEEN 1 AND 16777216),
    ADD CONSTRAINT raid_replay_storage_encoding_valid
        CHECK (storage_encoding IN ('identity', 'gzip')),
    ADD CONSTRAINT raid_replay_storage_mode_valid
        CHECK (
            (storage_provider = 'postgres-jsonb'
             AND command_stream IS NOT NULL
             AND storage_encoding = 'identity'
             AND jsonb_typeof(command_stream) = 'array'
             AND jsonb_array_length(command_stream) = command_count)
            OR
            (storage_provider <> 'postgres-jsonb'
             AND command_stream IS NULL
             AND storage_encoding = 'gzip')
        );

CREATE INDEX raid_replay_storage_lookup_idx
    ON splice.raid_replays(storage_provider, storage_key);

COMMIT;
