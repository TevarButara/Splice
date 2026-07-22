BEGIN;

ALTER TABLE splice.content_definitions
    DROP CONSTRAINT content_kind_valid,
    ADD CONSTRAINT content_kind_valid
        CHECK (content_kind IN ('TOWER', 'GARRISON', 'MINER', 'HERO', 'GEAR')),
    ADD COLUMN combat_payload jsonb NOT NULL DEFAULT '{}'::jsonb;

CREATE TABLE splice.player_heroes (
    player_id uuid NOT NULL REFERENCES splice.players(id),
    hero_content_id text NOT NULL,
    content_kind text NOT NULL DEFAULT 'HERO' CHECK (content_kind='HERO'),
    level integer NOT NULL DEFAULT 1 CHECK (level BETWEEN 1 AND 100),
    experience bigint NOT NULL DEFAULT 0 CHECK (experience >= 0),
    unlocked_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    PRIMARY KEY (player_id, hero_content_id),
    FOREIGN KEY (hero_content_id, content_kind)
        REFERENCES splice.content_definitions(content_id, content_kind)
);

CREATE TABLE splice.player_gear_items (
    id uuid PRIMARY KEY,
    owner_player_id uuid NOT NULL REFERENCES splice.players(id),
    gear_content_id text NOT NULL,
    content_kind text NOT NULL DEFAULT 'GEAR' CHECK (content_kind='GEAR'),
    level integer NOT NULL DEFAULT 1 CHECK (level BETWEEN 1 AND 100),
    acquired_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT player_gear_owner_unique UNIQUE (id, owner_player_id),
    FOREIGN KEY (gear_content_id, content_kind)
        REFERENCES splice.content_definitions(content_id, content_kind)
);

CREATE INDEX player_gear_owner_idx
    ON splice.player_gear_items(owner_player_id, acquired_at DESC);

ALTER TABLE splice.attacker_loadouts
    RENAME COLUMN raid_power TO army_power;
ALTER TABLE splice.attacker_loadouts
    ADD COLUMN hero_power bigint NOT NULL DEFAULT 0 CHECK (hero_power >= 0),
    ADD COLUMN gear_power bigint NOT NULL DEFAULT 0 CHECK (gear_power >= 0),
    ADD COLUMN raid_power bigint GENERATED ALWAYS AS
        (army_power + hero_power + gear_power) STORED,
    ADD COLUMN hero_payload jsonb NOT NULL DEFAULT '{}'::jsonb,
    ADD COLUMN gear_items jsonb NOT NULL DEFAULT '[]'::jsonb;

ALTER TABLE splice.attacker_loadout_snapshots
    RENAME COLUMN raid_power TO army_power;
ALTER TABLE splice.attacker_loadout_snapshots
    ADD COLUMN hero_power bigint NOT NULL DEFAULT 0 CHECK (hero_power >= 0),
    ADD COLUMN gear_power bigint NOT NULL DEFAULT 0 CHECK (gear_power >= 0),
    ADD COLUMN raid_power bigint GENERATED ALWAYS AS
        (army_power + hero_power + gear_power) STORED,
    ADD COLUMN hero_payload jsonb NOT NULL DEFAULT '{}'::jsonb,
    ADD COLUMN gear_items jsonb NOT NULL DEFAULT '[]'::jsonb;

COMMIT;
