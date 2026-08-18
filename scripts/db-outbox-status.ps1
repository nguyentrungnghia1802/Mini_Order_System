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

$sql = @'
SELECT
    COUNT(*) FILTER (WHERE published_at_utc IS NULL AND dead_lettered_at_utc IS NULL) AS pending_count,
    MIN(created_at_utc) FILTER (WHERE published_at_utc IS NULL AND dead_lettered_at_utc IS NULL) AS oldest_pending_at_utc,
    COUNT(*) FILTER (WHERE dead_lettered_at_utc IS NOT NULL) AS dead_lettered_count
FROM outbox_messages;

SELECT id, message_type, aggregate_id, attempt_count, next_attempt_at_utc,
       locked_until_utc, published_at_utc, dead_lettered_at_utc, last_error
FROM outbox_messages
WHERE published_at_utc IS NULL
   OR dead_lettered_at_utc IS NOT NULL
ORDER BY next_attempt_at_utc, created_at_utc, id
LIMIT 20;
'@

$remoteCommand = 'PGPASSWORD="$POSTGRES_PASSWORD" psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$ORDER_DB_NAME" -c "' + $sql.Replace('"', '\"') + '"'
$composeArgs = @(
    '--env-file', $envFilePath,
    '--file', $composeFilePath,
    'exec',
    '--no-tty',
    'postgres',
    'sh',
    '-c',
    $remoteCommand
)

& docker compose @composeArgs
if ($LASTEXITCODE -ne 0) {
    throw "Outbox status query failed with exit code $LASTEXITCODE"
}
