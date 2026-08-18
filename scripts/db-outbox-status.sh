#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
env_file="${1:-.env}"
compose_file="${2:-deploy/compose.yaml}"

if [[ "$env_file" != /* ]]; then
  env_file="$repo_root/$env_file"
fi

if [[ "$compose_file" != /* ]]; then
  compose_file="$repo_root/$compose_file"
fi

[[ -f "$env_file" ]] || { echo "Compose env file was not found: $env_file" >&2; exit 1; }
[[ -f "$compose_file" ]] || { echo "Compose file was not found: $compose_file" >&2; exit 1; }

read -r -d '' sql <<'SQL' || true
SELECT
    COUNT(*) FILTER (WHERE published_at_utc IS NULL AND dead_lettered_at_utc IS NULL) AS pending_count,
    MIN(created_at_utc) FILTER (WHERE published_at_utc IS NULL AND dead_lettered_at_utc IS NULL) AS oldest_pending_at_utc,
    COUNT(*) FILTER (WHERE dead_lettered_at_utc IS NOT NULL) AS dead_lettered_count
FROM outbox_messages;

SELECT id, message_type, aggregate_id, attempt_count, next_attempt_at_utc,
       locked_until_utc, published_at_utc, dead_lettered_at_utc, last_error
FROM outbox_messages
WHERE published_at_utc IS NULL
   OR dead_lettered_at_utc IS NOT NULL
ORDER BY next_attempt_at_utc, created_at_utc, id
LIMIT 20;
SQL

docker compose --env-file "$env_file" --file "$compose_file" exec --no-tty postgres \
  sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$ORDER_DB_NAME" -c "$1"' \
  sh "$sql"
