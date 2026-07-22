BEGIN;

-- Unity's existing composite id namespace is separated by resolver type: a tower and a card may
-- legitimately both be "faction/1". Backend identity therefore includes content_kind.
ALTER TABLE splice.content_definitions
    DROP CONSTRAINT content_definitions_pkey,
    ADD CONSTRAINT content_definitions_pkey PRIMARY KEY (content_id, content_kind);

COMMIT;
