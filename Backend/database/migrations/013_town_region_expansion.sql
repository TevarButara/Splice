BEGIN;

CREATE TABLE splice.town_region_unlocks (
    town_id uuid NOT NULL REFERENCES splice.towns(id),
    region_id text NOT NULL,
    map_template_id text NOT NULL,
    map_version integer NOT NULL CHECK (map_version > 0),
    gold_cost bigint NOT NULL CHECK (gold_cost >= 0),
    purchase_transaction_id uuid NOT NULL UNIQUE REFERENCES splice.ledger_transactions(id),
    unlocked_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    PRIMARY KEY (town_id, region_id),
    CONSTRAINT town_region_id_format CHECK (region_id ~ '^[a-z0-9][a-z0-9-]{0,63}$')
);

CREATE INDEX town_region_unlocks_map_idx
    ON splice.town_region_unlocks(map_template_id, map_version, region_id);

CREATE TRIGGER town_region_unlocks_immutable
BEFORE UPDATE OR DELETE ON splice.town_region_unlocks
FOR EACH ROW EXECUTE FUNCTION splice.reject_immutable_town_record_change();

COMMIT;
