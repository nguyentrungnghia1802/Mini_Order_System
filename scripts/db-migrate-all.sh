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

for service in migrate-product migrate-order migrate-notification; do
  echo "Running $service"
  docker compose --env-file "$env_file" --file "$compose_file" run --rm "$service"
done
