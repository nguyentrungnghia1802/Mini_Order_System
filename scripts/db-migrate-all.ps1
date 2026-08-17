[CmdletBinding()]
param(
    [string]$EnvFile = '.env',
    [string]$ComposeFile = 'deploy/compose.yaml'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$envFilePath = if ([IO.Path]::IsPathRooted($EnvFile)) { $EnvFile } else { Join-Path $repoRoot $EnvFile }
$composeFilePath = if ([IO.Path]::IsPathRooted($ComposeFile)) { $ComposeFile } else { Join-Path $repoRoot $ComposeFile }

if (-not (Test-Path -LiteralPath $envFilePath -PathType Leaf)) {
    throw "Compose env file was not found: $envFilePath"
}

if (-not (Test-Path -LiteralPath $composeFilePath -PathType Leaf)) {
    throw "Compose file was not found: $composeFilePath"
}

foreach ($service in @('migrate-product', 'migrate-order', 'migrate-notification')) {
    Write-Host "Running $service"
    $composeArgs = @('--env-file', $envFilePath, '--file', $composeFilePath, 'run', '--rm', $service)
    & docker compose @composeArgs
    if ($LASTEXITCODE -ne 0) {
        throw "$service failed with exit code $LASTEXITCODE"
    }
}
