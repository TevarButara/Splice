\set ON_ERROR_STOP on
SET search_path = splice, public;

INSERT INTO players (id, display_name)
VALUES ('11000000-0000-0000-0000-000000000011', 'C4C2E Load Player');

INSERT INTO ledger_accounts (id, account_key, owner_type, owner_id, currency_code)
VALUES ('21000000-0000-0000-0000-000000000011', 'test:c4c2e:load-player',
        'PLAYER', '11000000-0000-0000-0000-000000000011', 'WAR_GEM');

SELECT post_ledger_transaction(
  'test:c4c2e:load-mint', 'TEST_MINT', 'TEST', NULL,
  jsonb_build_array(
    jsonb_build_object('account_id','00000000-0000-0000-0000-000000000201','amount',-1000),
    jsonb_build_object('account_id','21000000-0000-0000-0000-000000000011','amount',1000)));
