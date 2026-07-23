#!/usr/bin/env bash
set -Eeuo pipefail

for migration in /opt/splice-db/migrations/*.sql; do
  psql --set ON_ERROR_STOP=1 --username "$POSTGRES_USER" \
    --dbname "$POSTGRES_DB" --file "$migration"
done

for seed in /opt/splice-db/seeds/*.sql; do
  psql --set ON_ERROR_STOP=1 --username "$POSTGRES_USER" \
    --dbname "$POSTGRES_DB" --file "$seed"
done
