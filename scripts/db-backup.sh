#!/usr/bin/env bash
set -euo pipefail

database="${1:-product}"
env_file="${2:-.env}"
compose_file="${3:-deploy/compose.yaml}"
output_directory="${4:-TestResults/backups}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

case "$database" in
  product) database_key="PRODUCT_DB_NAME" ;;
  order) database_key="ORDER_DB_NAME" ;;
  notification) database_key="NOTIFICATION_DB_NAME" ;;
  *) echo "Database must be product, order, or notification." >&2; exit 1 ;;
esac

[[ "$env_file" = /* ]] || env_file="$repo_root/$env_file"
[[ "$compose_file" = /* ]] || compose_file="$repo_root/$compose_file"
[[ -f "$env_file" ]] || { echo "Compose env file was not found: $env_file" >&2; exit 1; }
[[ -f "$compose_file" ]] || { echo "Compose file was not found: $compose_file" >&2; exit 1; }

read_env_value() {
  sed -n "s/^${1}=//p" "$env_file" | head -n 1
}

database_name="$(read_env_value "$database_key")"
[[ "$database_name" =~ ^[A-Za-z0-9_]+$ ]] || { echo "Unsafe database identifier: $database_name" >&2; exit 1; }
mkdir -p "$repo_root/$output_directory"
output_directory="$repo_root/$output_directory"

compose=(docker compose --env-file "$env_file" --file "$compose_file")
"${compose[@]}" up -d --wait postgres
container_id="$("${compose[@]}" ps -q postgres)"
[[ -n "$container_id" ]] || { echo 'PostgreSQL Compose container is not available.' >&2; exit 1; }

remote_file="microshop-$database-$(date -u +%Y%m%d-%H%M%S)-$$.dump"
remote_path="/tmp/$remote_file"
output_path="$output_directory/microshop-$database-$(date -u +%Y%m%d-%H%M%S).dump"
cleanup() { "${compose[@]}" exec -T postgres rm -f "$remote_path" >/dev/null 2>&1 || true; }
trap cleanup EXIT

"${compose[@]}" exec -T postgres sh -c "PGPASSWORD=\"\$POSTGRES_PASSWORD\" pg_dump --format=custom --no-owner --no-privileges --username \"\$POSTGRES_USER\" --dbname \"$database_name\" > \"$remote_path\""
docker cp "$container_id:$remote_path" "$output_path"
echo "Backup created: $output_path"
