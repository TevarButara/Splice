\set ON_ERROR_STOP on

-- Order-independent, constant-memory fingerprints for every table in the Splice schema.
-- Two independent hash seeds make accidental value drift detectable without materializing
-- an entire production table in string_agg.
SELECT format(
    'SELECT %L, count(*)::text, ' ||
    'COALESCE(sum(hashtextextended(to_jsonb(row_data)::text, 0)::numeric), 0)::text, ' ||
    'COALESCE(sum(hashtextextended(to_jsonb(row_data)::text, 76412026)::numeric), 0)::text ' ||
    'FROM splice.%I AS row_data;',
    tablename, tablename)
  FROM pg_catalog.pg_tables
 WHERE schemaname = 'splice'
 ORDER BY tablename
\gexec
