[CmdletBinding()]
param(
    [ValidateSet('product', 'order', 'notification')]
    [string]$Database = 'product',
    [string]$EnvFile = '.env',
    [string]$ComposeFile = 'deploy/compose.yaml',
    [string]$OutputDirectory = 'TestResults/backups'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$envFilePath = if ([IO.Path]::IsPathRooted($EnvFile)) { $EnvFile } else { Join-Path $repoRoot $EnvFile }
$composeFilePath = if ([IO.Path]::IsPathRooted($ComposeFile)) { $ComposeFile } else { Join-Path $repoRoot $ComposeFile }
$outputDirectoryPath = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $repoRoot $OutputDirectory }

foreach ($path in @($envFilePath, $composeFilePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required file was not found: $path"
    }
}

New-Item -ItemType Directory -Path $outputDirectoryPath -Force | Out-Null
$composePrefix = @('--env-file', $envFilePath, '--file', $composeFilePath)

Write-Host 'Starting only PostgreSQL if it is not already running; existing named volumes are preserved.'
& docker compose @composePrefix up -d --wait postgres
if ($LASTEXITCODE -ne 0) {
    throw "PostgreSQL startup failed with exit code $LASTEXITCODE"
}

$configJson = (& docker compose @composePrefix config --format json | Out-String)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to read the resolved Compose configuration.'
}
$config = $configJson | ConvertFrom-Json
$postgresEnvironment = $config.services.postgres.environment
$databaseKey = @{
    product = 'PRODUCT_DB_NAME'
    order = 'ORDER_DB_NAME'
    notification = 'NOTIFICATION_DB_NAME'
}[$Database]
$databaseName = [string]$postgresEnvironment.$databaseKey
if ($databaseName -notmatch '^[A-Za-z0-9_]+$') {
    throw "Resolved database name is not a safe PostgreSQL identifier: $databaseName"
}

$containerId = ((& docker compose @composePrefix ps -q postgres) | Select-Object -First 1).Trim()
if ([string]::IsNullOrWhiteSpace($containerId)) {
    throw 'The PostgreSQL Compose container is not available.'
}

$remoteFile = "microshop-$Database-$([guid]::NewGuid().ToString('N')).dump"
$remotePath = "/tmp/$remoteFile"
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outputPath = Join-Path $outputDirectoryPath "microshop-$Database-$timestamp.dump"

try {
    $dumpCommand = 'PGPASSWORD="$POSTGRES_PASSWORD" pg_dump --format=custom --no-owner --no-privileges --username "$POSTGRES_USER" --dbname "' + $databaseName + '" > "' + $remotePath + '"'
    & docker compose @composePrefix exec -T postgres sh -c $dumpCommand
    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump failed with exit code $LASTEXITCODE"
    }

    & docker cp "${containerId}:$remotePath" $outputPath
    if ($LASTEXITCODE -ne 0) {
        throw "docker cp failed with exit code $LASTEXITCODE"
    }
}
finally {
    & docker compose @composePrefix exec -T postgres rm -f $remotePath 2>$null | Out-Null
}

Write-Host "Backup created: $outputPath"
