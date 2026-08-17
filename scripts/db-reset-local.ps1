[CmdletBinding()]
param(
    [string]$EnvFile = '.env',
    [string]$ComposeFile = 'deploy/compose.yaml',
    [switch]$ConfirmReset,
    [switch]$AllowVolumeDeletion,
    [switch]$AllowNonDevelopment
)

$ErrorActionPreference = 'Stop'

if (-not $ConfirmReset -or -not $AllowVolumeDeletion) {
    throw 'Refusing local reset. Supply both -ConfirmReset and -AllowVolumeDeletion after explicit owner approval.'
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$envFilePath = if ([IO.Path]::IsPathRooted($EnvFile)) { $EnvFile } else { Join-Path $repoRoot $EnvFile }
$composeFilePath = if ([IO.Path]::IsPathRooted($ComposeFile)) { $ComposeFile } else { Join-Path $repoRoot $ComposeFile }

if (-not (Test-Path -LiteralPath $envFilePath -PathType Leaf)) {
    throw "Compose env file was not found: $envFilePath"
}

if (-not (Test-Path -LiteralPath $composeFilePath -PathType Leaf)) {
    throw "Compose file was not found: $composeFilePath"
}

$environmentLine = Select-String -Path $envFilePath -Pattern '^\s*ASPNETCORE_ENVIRONMENT\s*=' | Select-Object -First 1
$environment = if ($environmentLine) { ($environmentLine.Line -split '=', 2)[1].Trim() } else { '' }
if ($environment -ne 'Development' -and -not $AllowNonDevelopment) {
    throw 'Refusing reset outside an explicitly marked Development environment. Supply -AllowNonDevelopment only after owner approval.'
}

$confirmation = Read-Host 'Type DELETE MICROSHOP LOCAL VOLUMES to continue'
if ($confirmation -cne 'DELETE MICROSHOP LOCAL VOLUMES') {
    throw 'Reset confirmation did not match; no Docker command was run.'
}

$composeArgs = @('--env-file', $envFilePath, '--file', $composeFilePath, 'down', '--volumes', '--remove-orphans')
& docker compose @composeArgs
if ($LASTEXITCODE -ne 0) {
    throw "Local reset failed with exit code $LASTEXITCODE"
}
