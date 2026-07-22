BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE SCHEMA IF NOT EXISTS splice;

CREATE TABLE splice.currencies (
    code text PRIMARY KEY,
    display_name text NOT NULL,
    is_premium boolean NOT NULL DEFAULT false,
    is_raid_stake boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT currency_code_format CHECK (code ~ '^[A-Z][A-Z0-9_]{1,31}$')
);

CREATE TABLE splice.players (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    external_subject text UNIQUE,
    display_name text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE splice.ledger_accounts (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_key text NOT NULL UNIQUE,
    owner_type text NOT NULL,
    owner_id uuid NULL,
    currency_code text NOT NULL REFERENCES splice.currencies(code),
    allow_negative boolean NOT NULL DEFAULT false,
    balance bigint NOT NULL DEFAULT 0,
    version bigint NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT ledger_owner_type_valid CHECK (owner_type IN ('PLAYER', 'SYSTEM', 'TOWN', 'RAID_ESCROW')),
    CONSTRAINT ledger_account_key_not_blank CHECK (btrim(account_key) <> ''),
    CONSTRAINT ledger_nonnegative_guard CHECK (allow_negative OR balance >= 0)
);

CREATE INDEX ledger_accounts_owner_idx
    ON splice.ledger_accounts(owner_type, owner_id, currency_code);

CREATE TABLE splice.ledger_transactions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    idempotency_key text NOT NULL UNIQUE,
    request_hash char(64) NOT NULL,
    transaction_type text NOT NULL,
    reference_type text NULL,
    reference_id uuid NULL,
    status text NOT NULL DEFAULT 'PENDING',
    reversal_of_transaction_id uuid NULL REFERENCES splice.ledger_transactions(id),
    metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    posted_at timestamptz NULL,
    CONSTRAINT ledger_idempotency_key_not_blank CHECK (btrim(idempotency_key) <> ''),
    CONSTRAINT ledger_transaction_status_valid CHECK (status IN ('PENDING', 'POSTED', 'REVERSED')),
    CONSTRAINT ledger_request_hash_format CHECK (request_hash ~ '^[0-9a-f]{64}$')
);

CREATE INDEX ledger_transactions_reference_idx
    ON splice.ledger_transactions(reference_type, reference_id);

CREATE TABLE splice.ledger_postings (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ledger_transaction_id uuid NOT NULL REFERENCES splice.ledger_transactions(id) ON DELETE RESTRICT,
    ledger_account_id uuid NOT NULL REFERENCES splice.ledger_accounts(id) ON DELETE RESTRICT,
    amount bigint NOT NULL,
    balance_after bigint NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT ledger_posting_nonzero CHECK (amount <> 0),
    CONSTRAINT ledger_one_posting_per_account UNIQUE (ledger_transaction_id, ledger_account_id)
);

CREATE INDEX ledger_postings_account_history_idx
    ON splice.ledger_postings(ledger_account_id, id DESC);

CREATE TABLE splice.idempotency_requests (
    scope text NOT NULL,
    idempotency_key text NOT NULL,
    request_hash char(64) NOT NULL,
    response_status integer NULL,
    response_body jsonb NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    expires_at timestamptz NULL,
    PRIMARY KEY (scope, idempotency_key),
    CONSTRAINT idempotency_scope_not_blank CHECK (btrim(scope) <> ''),
    CONSTRAINT idempotency_request_hash_format CHECK (request_hash ~ '^[0-9a-f]{64}$')
);

CREATE INDEX idempotency_requests_expiry_idx
    ON splice.idempotency_requests(expires_at) WHERE expires_at IS NOT NULL;

CREATE TABLE splice.outbox_events (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    aggregate_type text NOT NULL,
    aggregate_id uuid NOT NULL,
    event_type text NOT NULL,
    payload jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    published_at timestamptz NULL
);

CREATE INDEX outbox_unpublished_idx
    ON splice.outbox_events(created_at) WHERE published_at IS NULL;

CREATE OR REPLACE FUNCTION splice.assert_posted_transaction_balanced()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    target_transaction_id uuid;
    target_status text;
    posting_count integer;
    posting_sum numeric;
BEGIN
    IF TG_TABLE_NAME = 'ledger_transactions' THEN
        target_transaction_id := COALESCE(NEW.id, OLD.id);
    ELSE
        target_transaction_id := COALESCE(NEW.ledger_transaction_id, OLD.ledger_transaction_id);
    END IF;

    SELECT status INTO target_status
    FROM splice.ledger_transactions
    WHERE id = target_transaction_id;

    IF target_status = 'POSTED' THEN
        SELECT count(*), COALESCE(sum(amount), 0)
          INTO posting_count, posting_sum
          FROM splice.ledger_postings
         WHERE ledger_transaction_id = target_transaction_id;

        IF posting_count < 2 OR posting_sum <> 0 THEN
            RAISE EXCEPTION 'LEDGER_UNBALANCED: transaction %, postings %, sum %',
                target_transaction_id, posting_count, posting_sum;
        END IF;
    END IF;

    RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER ledger_transaction_balance_guard
AFTER INSERT OR UPDATE OF status ON splice.ledger_transactions
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION splice.assert_posted_transaction_balanced();

CREATE CONSTRAINT TRIGGER ledger_posting_balance_guard
AFTER INSERT OR UPDATE OR DELETE ON splice.ledger_postings
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION splice.assert_posted_transaction_balanced();

COMMIT;
