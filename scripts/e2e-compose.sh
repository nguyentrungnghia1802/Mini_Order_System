#!/usr/bin/env bash
set -euo pipefail

env_file="${1:-.env.example}"
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ui_directory="$repository_root/web/microshop-ui"

cd "$repository_root"
docker compose --env-file "$env_file" -f deploy/compose.yaml config --quiet
docker compose --env-file "$env_file" -f deploy/compose.yaml up --build -d --wait

(
  cd "$ui_directory"
  npm ci
  npm run e2e:install
  npm run e2e
)
