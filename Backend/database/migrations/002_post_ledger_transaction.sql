BEGIN;

CREATE OR REPLACE FUNCTION splice.post_ledger_transaction(
    p_idempotency_key text,
    p_transaction_type text,
    p_reference_type text,
    p_reference_id uuid,
    p_postings jsonb,
    p_metadata jsonb DEFAULT '{}'::jsonb
)
RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = splice, pg_temp
AS $$
DECLARE
    transaction_id uuid;
    existing_hash char(64);
    calculated_hash char(64);
    canonical_postings jsonb;
    posting_count integer;
    account_count integer;
    currency_count integer;
    posting_sum numeric;
    posting_record record;
    account_record record;
    next_balance bigint;
BEGIN
    IF p_idempotency_key IS NULL OR btrim(p_idempotency_key) = '' THEN
        RAISE EXCEPTION 'IDEMPOTENCY_KEY_REQUIRED';
    END IF;
    IF p_transaction_type IS NULL OR btrim(p_transaction_type) = '' THEN
        RAISE EXCEPTION 'TRANSACTION_TYPE_REQUIRED';
    END IF;
    IF p_postings IS NULL OR jsonb_typeof(p_postings) <> 'array' THEN
        RAISE EXCEPTION 'POSTINGS_MUST_BE_ARRAY';
    END IF;

    SELECT jsonb_agg(
               jsonb_build_object('account_id', account_id::text, 'amount', amount)
               ORDER BY account_id
           ),
           count(*),
           COALESCE(sum(amount), 0)
      INTO canonical_postings, posting_count, posting_sum
      FROM (
          SELECT (item->>'account_id')::uuid AS account_id,
                 (item->>'amount')::bigint AS amount
            FROM jsonb_array_elements(p_postings) AS item
      ) parsed;

    IF posting_count < 2 THEN
        RAISE EXCEPTION 'LEDGER_REQUIRES_AT_LEAST_TWO_POSTINGS';
    END IF;
    IF posting_sum <> 0 THEN
        RAISE EXCEPTION 'LEDGER_UNBALANCED: sum %', posting_sum;
    END IF;
    IF EXISTS (
        SELECT 1
          FROM jsonb_array_elements(p_postings) item
         WHERE (item->>'amount')::bigint = 0
    ) THEN
        RAISE EXCEPTION 'ZERO_POSTING_NOT_ALLOWED';
    END IF;
    IF posting_count <> (
        SELECT count(DISTINCT (item->>'account_id')::uuid)
          FROM jsonb_array_elements(p_postings) item
    ) THEN
        RAISE EXCEPTION 'DUPLICATE_LEDGER_ACCOUNT';
    END IF;

    calculated_hash := encode(public.digest(
        concat_ws('|',
            p_transaction_type,
            COALESCE(p_reference_type, ''),
            COALESCE(p_reference_id::text, ''),
            canonical_postings::text,
            COALESCE(p_metadata, '{}'::jsonb)::text
        ),
        'sha256'
    ), 'hex');

    -- Same idempotency key is serialized even before a row exists.
    PERFORM pg_advisory_xact_lock(hashtextextended(p_idempotency_key, 0));

    SELECT id, request_hash
      INTO transaction_id, existing_hash
      FROM ledger_transactions
     WHERE idempotency_key = p_idempotency_key;

    IF transaction_id IS NOT NULL THEN
        IF existing_hash <> calculated_hash THEN
            RAISE EXCEPTION 'IDEMPOTENCY_KEY_REUSED';
        END IF;
        RETURN transaction_id;
    END IF;

    SELECT count(*), count(DISTINCT a.currency_code)
      INTO account_count, currency_count
      FROM ledger_accounts a
      JOIN (
          SELECT (item->>'account_id')::uuid AS account_id
            FROM jsonb_array_elements(p_postings) item
      ) requested ON requested.account_id = a.id;

    IF account_count <> posting_count THEN
        RAISE EXCEPTION 'LEDGER_ACCOUNT_NOT_FOUND';
    END IF;
    IF currency_count <> 1 THEN
        RAISE EXCEPTION 'CURRENCY_MISMATCH';
    END IF;

    -- Deterministic lock order prevents deadlocks between opposite transfers.
    FOR account_record IN
        SELECT a.id
          FROM ledger_accounts a
          JOIN (
              SELECT (item->>'account_id')::uuid AS account_id
                FROM jsonb_array_elements(p_postings) item
          ) requested ON requested.account_id = a.id
         ORDER BY a.id
         FOR UPDATE OF a
    LOOP
        NULL;
    END LOOP;

    INSERT INTO ledger_transactions (
        idempotency_key, request_hash, transaction_type,
        reference_type, reference_id, metadata
    ) VALUES (
        p_idempotency_key, calculated_hash, p_transaction_type,
        p_reference_type, p_reference_id, COALESCE(p_metadata, '{}'::jsonb)
    ) RETURNING id INTO transaction_id;

    FOR posting_record IN
        SELECT (item->>'account_id')::uuid AS account_id,
               (item->>'amount')::bigint AS amount
          FROM jsonb_array_elements(p_postings) item
         ORDER BY (item->>'account_id')::uuid
    LOOP
        SELECT balance, allow_negative
          INTO account_record
          FROM ledger_accounts
         WHERE id = posting_record.account_id
         FOR UPDATE;

        next_balance := account_record.balance + posting_record.amount;
        IF NOT account_record.allow_negative AND next_balance < 0 THEN
            RAISE EXCEPTION 'INSUFFICIENT_FUNDS: account %', posting_record.account_id;
        END IF;

        UPDATE ledger_accounts
           SET balance = next_balance,
               version = version + 1,
               updated_at = clock_timestamp()
         WHERE id = posting_record.account_id;

        INSERT INTO ledger_postings (
            ledger_transaction_id, ledger_account_id, amount, balance_after
        ) VALUES (
            transaction_id, posting_record.account_id, posting_record.amount, next_balance
        );
    END LOOP;

    UPDATE ledger_transactions
       SET status = 'POSTED', posted_at = clock_timestamp()
     WHERE id = transaction_id;

    INSERT INTO outbox_events (aggregate_type, aggregate_id, event_type, payload)
    VALUES (
        'LEDGER_TRANSACTION', transaction_id, 'LedgerTransactionPosted',
        jsonb_build_object('transactionId', transaction_id, 'transactionType', p_transaction_type)
    );

    RETURN transaction_id;
END;
$$;

REVOKE ALL ON FUNCTION splice.post_ledger_transaction(text, text, text, uuid, jsonb, jsonb) FROM PUBLIC;

COMMIT;
