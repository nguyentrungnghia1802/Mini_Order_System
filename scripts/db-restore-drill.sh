#!/usr/bin/env bash
set -euo pipefail

if command -v pwsh >/dev/null 2>&1; then
  script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  exec pwsh -NoLogo -NoProfile -File "$script_dir/db-restore-drill.ps1" "$@"
fi

echo 'db-restore-drill.sh requires PowerShell 7 (pwsh) for safe binary dump handling.' >&2
exit 1
