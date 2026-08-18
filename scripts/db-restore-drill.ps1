[CmdletBinding()]
param(
    [ValidateSet('product', 'order', 'notification')]
    [string]$Database = 'product',
    [string]$EnvFile = '.env.example',
    [string]$ComposeFile = 'deploy/compose.yaml'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$envFilePath = if ([IO.Path]::IsPathRooted($EnvFile)) { $EnvFile } else { Join-Path $repoRoot $EnvFile }
$composeFilePath = if ([IO.Path]::IsPathRooted($ComposeFile)) { $ComposeFile } else { Join-Path $repoRoot $ComposeFile }
$workDirectory = Join-Path $repoRoot 'TestResults/restore-drill'

foreach ($path in @($envFilePath, $composeFilePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required file was not found: $path"
    }
}

New-Item -ItemType Directory -Path $workDirectory -Force | Out-Null
$composePrefix = @('--env-file', $envFilePath, '--file', $composeFilePath)
$databaseKey = @{
    product = 'PRODUCT_DB_NAME'
    order = 'ORDER_DB_NAME'
    notification = 'NOTIFICATION_DB_NAME'
}[$Database]
$tableName = @{
    product = 'products'
    order = 'orders'
    notification = 'notifications'
}[$Database]

& docker compose @composePrefix up -d --wait postgres
if ($LASTEXITCODE -ne 0) {
    throw "PostgreSQL startup failed with exit code $LASTEXITCODE"
}

$configJson = (& docker compose @composePrefix config --format json | Out-String)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to read the resolved Compose configuration.'
}
$config = $configJson | ConvertFrom-Json
$databaseName = [string]$config.services.postgres.environment.$databaseKey
if ($databaseName -notmatch '^[A-Za-z0-9_]+$') {
    throw "Resolved database name is not a safe PostgreSQL identifier: $databaseName"
}

$sourceContainerId = ((& docker compose @composePrefix ps -q postgres) | Select-Object -First 1).Trim()
if ([string]::IsNullOrWhiteSpace($sourceContainerId)) {
    throw 'The PostgreSQL Compose container is not available.'
}

$drillId = [guid]::NewGuid().ToString('N')
$remoteFile = "microshop-restore-$drillId.dump"
$remotePath = "/tmp/$remoteFile"
$localDump = Join-Path $workDirectory "$remoteFile"
$restoreContainer = "microshop-restore-drill-$drillId"
$restorePassword = [guid]::NewGuid().ToString('N')

try {
    $dumpCommand = 'PGPASSWORD="$POSTGRES_PASSWORD" pg_dump --format=custom --no-owner --no-privileges --username "$POSTGRES_USER" --dbname "' + $databaseName + '" > "' + $remotePath + '"'
    & docker compose @composePrefix exec -T postgres sh -c $dumpCommand
    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump failed with exit code $LASTEXITCODE"
    }

    & docker cp "${sourceContainerId}:$remotePath" $localDump
    if ($LASTEXITCODE -ne 0) {
        throw "docker cp failed with exit code $LASTEXITCODE"
    }

    & docker run --detach --name $restoreContainer --network none --tmpfs '/var/lib/postgresql/data:rw,noexec,nosuid,size=256m' --env "POSTGRES_PASSWORD=$restorePassword" --env 'POSTGRES_DB=microshop_restore' postgres:17-alpine | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "restore PostgreSQL container failed with exit code $LASTEXITCODE"
    }

    $deadline = (Get-Date).AddSeconds(90)
    do {
        & docker exec $restoreContainer pg_isready -U postgres -d microshop_restore 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { break }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    if ($LASTEXITCODE -ne 0) {
        throw 'The disposable restore PostgreSQL did not become ready within 90 seconds.'
    }

    & docker cp $localDump "${restoreContainer}:/tmp/$remoteFile"
    if ($LASTEXITCODE -ne 0) {
        throw "docker cp to restore container failed with exit code $LASTEXITCODE"
    }
    & docker exec $restoreContainer pg_restore --username postgres --exit-on-error --no-owner --no-privileges --dbname microshop_restore "/tmp/$remoteFile"
    if ($LASTEXITCODE -ne 0) {
        throw "pg_restore failed with exit code $LASTEXITCODE"
    }

    $count = (& docker exec $restoreContainer psql -U postgres -d microshop_restore -Atqc "SELECT COUNT(*) FROM $tableName;").Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Restored table verification failed with exit code $LASTEXITCODE"
    }
    Write-Host "Restore drill passed for $Database database; restored $tableName row count: $count."
}
finally {
    & docker compose @composePrefix exec -T postgres rm -f $remotePath 2>$null | Out-Null
    & docker rm -f $restoreContainer 2>$null | Out-Null
    if (Test-Path -LiteralPath $localDump -PathType Leaf) {
        Remove-Item -LiteralPath $localDump -Force
    }
}
