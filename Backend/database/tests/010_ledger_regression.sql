\set ON_ERROR_STOP on

SET search_path = splice, public;

INSERT INTO players (id, display_name) VALUES
    ('10000000-0000-0000-0000-000000000001', 'C1 Test Player A'),
    ('10000000-0000-0000-0000-000000000002', 'C1 Test Player B')
ON CONFLICT (id) DO NOTHING;

INSERT INTO ledger_accounts (id, account_key, owner_type, owner_id, currency_code)
VALUES
    ('20000000-0000-0000-0000-000000000001', 'test:player-a:war-gem', 'PLAYER', '10000000-0000-0000-0000-000000000001', 'WAR_GEM'),
    ('20000000-0000-0000-0000-000000000002', 'test:player-b:war-gem', 'PLAYER', '10000000-0000-0000-0000-000000000002', 'WAR_GEM')
ON CONFLICT (id) DO NOTHING;

DO $$
DECLARE
    first_transaction uuid;
    replay_transaction uuid;
    reuse_rejected boolean := false;
    unbalanced_rejected boolean := false;
    balance_a bigint;
    balance_b bigint;
BEGIN
    first_transaction := post_ledger_transaction(
        'test:mint:a:1000', 'TEST_MINT', 'TEST', NULL,
        jsonb_build_array(
            jsonb_build_object('account_id', '00000000-0000-0000-0000-000000000201', 'amount', -1000),
            jsonb_build_object('account_id', '20000000-0000-0000-0000-000000000001', 'amount', 1000)
        ),
        '{"source":"c1-regression"}'::jsonb
    );

    replay_transaction := post_ledger_transaction(
        'test:mint:a:1000', 'TEST_MINT', 'TEST', NULL,
        jsonb_build_array(
            jsonb_build_object('account_id', '00000000-0000-0000-0000-000000000201', 'amount', -1000),
            jsonb_build_object('account_id', '20000000-0000-0000-0000-000000000001', 'amount', 1000)
        ),
        '{"source":"c1-regression"}'::jsonb
    );

    IF first_transaction <> replay_transaction THEN
        RAISE EXCEPTION 'TEST_FAILED: same request did not replay same transaction';
    END IF;

    SELECT balance INTO balance_a FROM ledger_accounts
     WHERE id = '20000000-0000-0000-0000-000000000001';
    IF balance_a <> 1000 THEN
        RAISE EXCEPTION 'TEST_FAILED: replay double-applied, balance %', balance_a;
    END IF;

    BEGIN
        PERFORM post_ledger_transaction(
            'test:mint:a:1000', 'TEST_MINT', 'TEST', NULL,
            jsonb_build_array(
                jsonb_build_object('account_id', '00000000-0000-0000-0000-000000000201', 'amount', -900),
                jsonb_build_object('account_id', '20000000-0000-0000-0000-000000000001', 'amount', 900)
            ),
            '{"source":"c1-regression"}'::jsonb
        );
    EXCEPTION WHEN OTHERS THEN
        reuse_rejected := position('IDEMPOTENCY_KEY_REUSED' IN SQLERRM) > 0;
    END;
    IF NOT reuse_rejected THEN
        RAISE EXCEPTION 'TEST_FAILED: changed idempotent request was accepted';
    END IF;

    PERFORM post_ledger_transaction(
        'test:transfer:a-to-b:250', 'TEST_TRANSFER', 'TEST', NULL,
        jsonb_build_array(
            jsonb_build_object('account_id', '20000000-0000-0000-0000-000000000001', 'amount', -250),
            jsonb_build_object('account_id', '20000000-0000-0000-0000-000000000002', 'amount', 250)
        )
    );

    SELECT balance INTO balance_a FROM ledger_accounts
     WHERE id = '20000000-0000-0000-0000-000000000001';
    SELECT balance INTO balance_b FROM ledger_accounts
     WHERE id = '20000000-0000-0000-0000-000000000002';
    IF balance_a <> 750 OR balance_b <> 250 THEN
        RAISE EXCEPTION 'TEST_FAILED: transfer balances are %, %', balance_a, balance_b;
    END IF;

    BEGIN
        INSERT INTO ledger_transactions (
            id, idempotency_key, request_hash, transaction_type, status
        ) VALUES (
            '30000000-0000-0000-0000-000000000001', 'test:invalid:unbalanced',
            repeat('a', 64), 'TEST_INVALID', 'POSTED'
        );
        INSERT INTO ledger_postings (
            ledger_transaction_id, ledger_account_id, amount, balance_after
        ) VALUES
            ('30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', -10, 740),
            ('30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000002', 9, 259);
        SET CONSTRAINTS ALL IMMEDIATE;
    EXCEPTION WHEN OTHERS THEN
        unbalanced_rejected := position('LEDGER_UNBALANCED' IN SQLERRM) > 0;
        SET CONSTRAINTS ALL DEFERRED;
    END;
    IF NOT unbalanced_rejected THEN
        RAISE EXCEPTION 'TEST_FAILED: direct unbalanced transaction was accepted';
    END IF;

    IF EXISTS (
        SELECT 1
          FROM ledger_transactions t
         WHERE t.status = 'POSTED'
           AND (SELECT COALESCE(sum(p.amount), 0)
                  FROM ledger_postings p
                 WHERE p.ledger_transaction_id = t.id) <> 0
    ) THEN
        RAISE EXCEPTION 'TEST_FAILED: posted ledger invariant is broken';
    END IF;
END;
$$;

SELECT 'ledger_regression: PASS';
