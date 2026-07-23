#!/usr/bin/env bash
set -Eeuo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
backend_dir="$(cd "$script_dir/../.." && pwd)"
compose_file="$backend_dir/compose.local-observability.yml"

docker compose --file "$compose_file" config --quiet

grep -Fq 'USER $APP_UID' "$backend_dir/Dockerfile"
[[ "$(grep -c '^FROM .*@sha256:' "$backend_dir/Dockerfile")" -eq 2 ]]
[[ "$(grep -c '^    image: .*@sha256:' "$compose_file")" -eq 3 ]]
grep -Fq '"127.0.0.1:5080:8080"' "$compose_file"
grep -Fq '"127.0.0.1:9090:9090"' "$compose_file"
grep -Fq '"127.0.0.1:9093:9093"' "$compose_file"
grep -Fq 'internal: true' "$compose_file"
grep -Fq 'read_only: true' "$compose_file"
grep -Fq 'no-new-privileges:true' "$compose_file"
if sed -n '/^  postgres:/,/^  [a-z]/p' "$compose_file" | grep -q 'ports:'; then
  echo "C4D1C_CONFIG_FAILED: PostgreSQL must not publish a host port." >&2
  exit 1
fi

echo "C4D1C container config tests: PASS (valid Compose, non-root API, loopback dashboards, isolated database)"

if [[ "${SPLICE_CONTAINER_E2E:-0}" != "1" ]]; then
  echo "C4D1C container E2E: SKIP (set SPLICE_CONTAINER_E2E=1 when Docker Desktop is running)"
  exit 0
fi

if ! docker info >/dev/null 2>&1; then
  echo "C4D1C_CONTAINER_FAILED: Docker daemon is unavailable." >&2
  exit 1
fi

cleanup() {
  docker compose --file "$compose_file" down --volumes --remove-orphans >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker compose --file "$compose_file" up --detach --build

ready=false
for _ in {1..60}; do
  if curl --fail --silent http://127.0.0.1:5080/health/ready >/dev/null &&
     curl --fail --silent http://127.0.0.1:9090/-/ready >/dev/null &&
     curl --fail --silent http://127.0.0.1:9093/-/ready >/dev/null &&
     curl --fail --silent 'http://127.0.0.1:9090/api/v1/targets' |
       grep -Fq '"health":"up"'; then
    ready=true
    break
  fi
  sleep 2
done

if [[ "$ready" != true ]]; then
  echo "C4D1C_CONTAINER_FAILED: stack did not become ready with a successful scrape." >&2
  docker compose --file "$compose_file" ps >&2
  docker compose --file "$compose_file" logs --no-color --tail=200 >&2
  exit 1
fi

api_container="$(docker compose --file "$compose_file" ps --quiet api)"
api_user="$(docker inspect --format '{{.Config.User}}' "$api_container")"
[[ -n "$api_user" && "$api_user" != "0" && "$api_user" != "root" ]]
[[ "$(docker inspect --format '{{.HostConfig.ReadonlyRootfs}}' "$api_container")" == "true" ]]
docker inspect --format '{{json .HostConfig.CapDrop}}' "$api_container" |
  grep -Fq '"ALL"'

curl --fail --silent http://127.0.0.1:5080/health/ready |
  grep -Fq '"status":"ok"'
if curl --fail --silent http://127.0.0.1:5080/metrics >/dev/null 2>&1; then
  echo "C4D1C_CONTAINER_FAILED: unauthenticated metrics request succeeded." >&2
  exit 1
fi
curl --fail --silent \
  --header 'Authorization: Bearer local-only-observability-token-change-before-sharing' \
  http://127.0.0.1:5080/metrics |
  grep -Fq 'splice_ops_healthy '

echo "C4D1C container E2E: PASS (API readiness, protected metrics, Prometheus scrape, Alertmanager)"
