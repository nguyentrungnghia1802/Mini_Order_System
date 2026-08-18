#!/usr/bin/env bash
set -euo pipefail

if ! command -v pwsh >/dev/null 2>&1; then
  echo "failure-injection.sh requires pwsh because the harness shares the repository's PowerShell HTTP/Compose checks." >&2
  exit 2
fi

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec pwsh -NoLogo -NoProfile -File "$script_directory/failure-injection.ps1" "$@"
