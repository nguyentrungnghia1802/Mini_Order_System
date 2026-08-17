#!/usr/bin/env bash
set -euo pipefail

env_file='.env'
compose_file='deploy/compose.yaml'
confirm_reset=false
allow_volume_deletion=false
allow_non_development=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env-file) env_file="$2"; shift 2 ;;
    --compose-file) compose_file="$2"; shift 2 ;;
    --confirm-reset) confirm_reset=true; shift ;;
    --allow-volume-deletion) allow_volume_deletion=true; shift ;;
    --allow-non-development) allow_non_development=true; shift ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

[[ "$confirm_reset" == true && "$allow_volume_deletion" == true ]] || {
  echo 'Refusing local reset. Supply --confirm-reset and --allow-volume-deletion after explicit owner approval.' >&2
  exit 1
}

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ "$env_file" != /* ]]; then env_file="$repo_root/$env_file"; fi
if [[ "$compose_file" != /* ]]; then compose_file="$repo_root/$compose_file"; fi
[[ -f "$env_file" ]] || { echo "Compose env file was not found: $env_file" >&2; exit 1; }
[[ -f "$compose_file" ]] || { echo "Compose file was not found: $compose_file" >&2; exit 1; }

environment="$(sed -n 's/^[[:space:]]*ASPNETCORE_ENVIRONMENT[[:space:]]*=[[:space:]]*//p' "$env_file" | head -n 1)"
if [[ "$environment" != 'Development' && "$allow_non_development" != true ]]; then
  echo 'Refusing reset outside an explicitly marked Development environment. Use --allow-non-development only after owner approval.' >&2
  exit 1
fi

read -r -p 'Type DELETE MICROSHOP LOCAL VOLUMES to continue: ' confirmation
[[ "$confirmation" == 'DELETE MICROSHOP LOCAL VOLUMES' ]] || {
  echo 'Reset confirmation did not match; no Docker command was run.' >&2
  exit 1
}

docker compose --env-file "$env_file" --file "$compose_file" down --volumes --remove-orphans
