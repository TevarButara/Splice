BEGIN;

ALTER TABLE splice.attacker_loadouts
    ADD COLUMN army_items jsonb NOT NULL DEFAULT '[]'::jsonb,
    ADD CONSTRAINT attacker_loadouts_army_items_array
        CHECK (jsonb_typeof(army_items) = 'array');

ALTER TABLE splice.attacker_loadout_snapshots
    ADD COLUMN army_items jsonb NOT NULL DEFAULT '[]'::jsonb,
    ADD CONSTRAINT attacker_loadout_snapshots_army_items_array
        CHECK (jsonb_typeof(army_items) = 'array');

COMMIT;
