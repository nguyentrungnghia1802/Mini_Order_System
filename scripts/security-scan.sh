#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
env_file="${1:-.env.example}"
compose_file="${2:-deploy/compose.yaml}"

if [[ "$env_file" != /* ]]; then
  env_file="$repo_root/$env_file"
fi

if [[ "$compose_file" != /* ]]; then
  compose_file="$repo_root/$compose_file"
fi

[[ -f "$env_file" ]] || { echo "Compose env file was not found: $env_file" >&2; exit 1; }
[[ -f "$compose_file" ]] || { echo "Compose file was not found: $compose_file" >&2; exit 1; }

dotnet restore "$repo_root/MicroShop.sln" --locked-mode
dotnet list "$repo_root/MicroShop.sln" package --vulnerable --include-transitive --no-restore
(cd "$repo_root/web/microshop-ui" && npm audit --omit=dev --audit-level=high)

compose=(docker compose --env-file "$env_file" --file "$compose_file")
"${compose[@]}" config --quiet
"${compose[@]}" build --quiet
while IFS= read -r image; do
  [[ -n "$image" ]] || continue
  [[ "$image" =~ ^(postgres|rabbitmq)(:|$) ]] && continue
  docker scout cves --only-severity critical,high --exit-code "local://$image"
done < <("${compose[@]}" config --images)

echo "Security scan passed: no NuGet/npm production or repository-built application-image HIGH/CRITICAL findings."
