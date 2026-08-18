[CmdletBinding()]
param(
    [string]$EnvFile = '.env.example',
    [switch]$SkipInstall
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$uiDirectory = Join-Path $repositoryRoot 'web/microshop-ui'
$composeArguments = @('--env-file', $EnvFile, '-f', 'deploy/compose.yaml')
$pinnedNodeDirectory = 'C:\WINDOWS\TEMP\microshop-node-v24.15.0-archive\node-v24.15.0-win-x64'
if (Test-Path (Join-Path $pinnedNodeDirectory 'node.exe')) {
    $env:Path = "$pinnedNodeDirectory;$env:Path"
}

function Invoke-Compose {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

    & docker compose @composeArguments @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repositoryRoot
try {
    Invoke-Compose config --quiet
    Invoke-Compose up --build -d --wait

    if (-not $SkipInstall) {
        Push-Location $uiDirectory
        try {
            & npm.cmd ci
            if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
            & npm.cmd run e2e:install
            if ($LASTEXITCODE -ne 0) { throw 'Playwright browser installation failed.' }
        }
        finally {
            Pop-Location
        }
    }

    Push-Location $uiDirectory
    try {
        & npm.cmd run e2e
        if ($LASTEXITCODE -ne 0) { throw 'Playwright Compose E2E failed.' }
    }
    finally {
        Pop-Location
    }
}
finally {
    Pop-Location
}
