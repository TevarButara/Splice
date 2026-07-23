\set ON_ERROR_STOP on

DO $integrity$
DECLARE
    violation_count bigint;
BEGIN
    SELECT count(*) INTO violation_count
      FROM (
        SELECT transaction_row.id
          FROM splice.ledger_transactions transaction_row
          LEFT JOIN splice.ledger_postings posting
            ON posting.ledger_transaction_id = transaction_row.id
         WHERE transaction_row.status = 'POSTED'
         GROUP BY transaction_row.id
        HAVING count(posting.id) < 2 OR COALESCE(sum(posting.amount), 0) <> 0
      ) violations;
    IF violation_count <> 0 THEN
        RAISE EXCEPTION 'C4D1_LEDGER_TRANSACTION_UNBALANCED: %', violation_count;
    END IF;

    SELECT count(*) INTO violation_count
      FROM splice.ledger_accounts account
      LEFT JOIN (
        SELECT ledger_account_id, sum(amount) AS posting_balance
          FROM splice.ledger_postings
         GROUP BY ledger_account_id
      ) totals ON totals.ledger_account_id = account.id
     WHERE account.balance <> COALESCE(totals.posting_balance, 0);
    IF violation_count <> 0 THEN
        RAISE EXCEPTION 'C4D1_LEDGER_ACCOUNT_DRIFT: %', violation_count;
    END IF;

    SELECT count(*) INTO violation_count
      FROM splice.ledger_accounts
     WHERE NOT allow_negative AND balance < 0;
    IF violation_count <> 0 THEN
        RAISE EXCEPTION 'C4D1_LEDGER_NEGATIVE_ACCOUNT: %', violation_count;
    END IF;

    SELECT count(*) INTO violation_count
      FROM splice.town_snapshots
     WHERE payload_sha256 <>
           encode(public.digest(payload::text, 'sha256'), 'hex');
    IF violation_count <> 0 THEN
        RAISE EXCEPTION 'C4D1_TOWN_SNAPSHOT_HASH_DRIFT: %', violation_count;
    END IF;

    SELECT count(*) INTO violation_count
      FROM splice.raid_escrows escrow
      JOIN splice.ledger_accounts account ON account.id = escrow.ledger_account_id
     WHERE account.balance <>
           CASE WHEN escrow.state IN ('FUNDED', 'ACTIVE')
                THEN escrow.funded_amount + escrow.defender_reserved_amount
                ELSE 0 END;
    IF violation_count <> 0 THEN
        RAISE EXCEPTION 'C4D1_RAID_ESCROW_DRIFT: %', violation_count;
    END IF;

END
$integrity$;

SELECT 'C4D1_INTEGRITY_PASS';
