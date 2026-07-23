\set ON_ERROR_STOP on

SELECT replay.storage_key, replay.storage_etag,
       replay.storage_content_length, replay.command_stream_hash,
       replay.tick_count, replay.command_count, result.outcome,
       result.breached_rings, replay.simulation_version,
       replay.storage_encoding
  FROM splice.raid_replays replay
  JOIN splice.raid_results result ON result.id = replay.result_id
 WHERE replay.storage_provider = 'local-filesystem'
 ORDER BY replay.storage_key;
