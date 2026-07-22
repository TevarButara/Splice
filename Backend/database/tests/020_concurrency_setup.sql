\set ON_ERROR_STOP on
SET search_path = splice, public;

INSERT INTO players (id, display_name) VALUES
    ('10000000-0000-0000-0000-000000000003', 'Concurrency Source'),
    ('10000000-0000-0000-0000-000000000004', 'Concurrency Target A'),
    ('10000000-0000-0000-0000-000000000005', 'Concurrency Target B')
ON CONFLICT (id) DO NOTHING;

INSERT INTO ledger_accounts (id, account_key, owner_type, owner_id, currency_code)
VALUES
    ('20000000-0000-0000-0000-000000000003', 'test:concurrency:source', 'PLAYER', '10000000-0000-0000-0000-000000000003', 'WAR_GEM'),
    ('20000000-0000-0000-0000-000000000004', 'test:concurrency:target-a', 'PLAYER', '10000000-0000-0000-0000-000000000004', 'WAR_GEM'),
    ('20000000-0000-0000-0000-000000000005', 'test:concurrency:target-b', 'PLAYER', '10000000-0000-0000-0000-000000000005', 'WAR_GEM')
ON CONFLICT (id) DO NOTHING;

SELECT post_ledger_transaction(
    'test:concurrency:fund:1000', 'TEST_MINT', 'TEST', NULL,
    jsonb_build_array(
        jsonb_build_object('account_id', '00000000-0000-0000-0000-000000000201', 'amount', -1000),
        jsonb_build_object('account_id', '20000000-0000-0000-0000-000000000003', 'amount', 1000)
    )
);
