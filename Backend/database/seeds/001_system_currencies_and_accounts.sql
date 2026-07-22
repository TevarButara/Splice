BEGIN;

INSERT INTO splice.currencies (code, display_name, is_premium, is_raid_stake)
VALUES
    ('GOLD', 'Gold', false, false),
    ('WAR_GEM', 'War Gem', false, true),
    ('PREMIUM_DIAMOND', 'Premium Diamond', true, false)
ON CONFLICT (code) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    is_premium = EXCLUDED.is_premium,
    is_raid_stake = EXCLUDED.is_raid_stake;

INSERT INTO splice.ledger_accounts (
    id, account_key, owner_type, owner_id, currency_code, allow_negative
)
VALUES
    ('00000000-0000-0000-0000-000000000101', 'system:gold:issuance', 'SYSTEM', NULL, 'GOLD', true),
    ('00000000-0000-0000-0000-000000000102', 'system:gold:sink', 'SYSTEM', NULL, 'GOLD', false),
    ('00000000-0000-0000-0000-000000000201', 'system:war_gem:issuance', 'SYSTEM', NULL, 'WAR_GEM', true),
    ('00000000-0000-0000-0000-000000000202', 'system:war_gem:sink', 'SYSTEM', NULL, 'WAR_GEM', false),
    ('00000000-0000-0000-0000-000000000301', 'system:premium_diamond:issuance', 'SYSTEM', NULL, 'PREMIUM_DIAMOND', true),
    ('00000000-0000-0000-0000-000000000302', 'system:premium_diamond:sink', 'SYSTEM', NULL, 'PREMIUM_DIAMOND', false)
ON CONFLICT (account_key) DO NOTHING;

COMMIT;
