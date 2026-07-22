\set ON_ERROR_STOP on
SET search_path = splice, public;

DO $$
DECLARE
    source_balance bigint;
    target_total bigint;
    successful_transfers integer;
BEGIN
    SELECT balance INTO source_balance
      FROM ledger_accounts
     WHERE id = '20000000-0000-0000-0000-000000000003';

    SELECT sum(balance) INTO target_total
      FROM ledger_accounts
     WHERE id IN (
        '20000000-0000-0000-0000-000000000004',
        '20000000-0000-0000-0000-000000000005'
     );

    SELECT count(*) INTO successful_transfers
      FROM ledger_transactions
     WHERE idempotency_key IN ('test:concurrency:transfer:a', 'test:concurrency:transfer:b')
       AND status = 'POSTED';

    IF successful_transfers <> 1 OR source_balance <> 300 OR target_total <> 700 THEN
        RAISE EXCEPTION 'TEST_FAILED: concurrency result tx=%, source=%, targets=%',
            successful_transfers, source_balance, target_total;
    END IF;

    IF source_balance < 0 THEN
        RAISE EXCEPTION 'TEST_FAILED: concurrent debit made source negative';
    END IF;
END;
$$;

SELECT 'ledger_concurrency: PASS';
