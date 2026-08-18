[CmdletBinding()]
param(
    [string]$EnvFile = '.env.example',
    [string]$ComposeFile = 'deploy/compose.yaml',
    [string]$Solution = 'MicroShop.sln',
    [switch]$SkipImageBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$envFilePath = if ([IO.Path]::IsPathRooted($EnvFile)) { $EnvFile } else { Join-Path $repoRoot $EnvFile }
$composeFilePath = if ([IO.Path]::IsPathRooted($ComposeFile)) { $ComposeFile } else { Join-Path $repoRoot $ComposeFile }
$solutionPath = if ([IO.Path]::IsPathRooted($Solution)) { $Solution } else { Join-Path $repoRoot $Solution }

foreach ($path in @($envFilePath, $composeFilePath, $solutionPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required file was not found: $path"
    }
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [scriptblock]$Action
    )

    Write-Host "[$Name]"
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$docker = (Get-Command docker -ErrorAction Stop).Source
$npmCommand = Get-Command npm.cmd -ErrorAction SilentlyContinue
if ($null -eq $npmCommand) {
    $npmCommand = Get-Command npm -ErrorAction Stop
}
$composePrefix = @('--env-file', $envFilePath, '--file', $composeFilePath)

Invoke-Checked -Name 'NuGet vulnerability audit' -Action {
    & $dotnet restore $solutionPath --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet restore failed with exit code $LASTEXITCODE"
    }
    & $dotnet list $solutionPath package --vulnerable --include-transitive --no-restore
}

Push-Location (Join-Path $repoRoot 'web/microshop-ui')
try {
    Invoke-Checked -Name 'npm production vulnerability audit' -Action {
        & $npmCommand.Source audit --omit=dev --audit-level=high
    }
}
finally {
    Pop-Location
}

Invoke-Checked -Name 'Compose configuration validation' -Action {
    & $docker compose @composePrefix config --quiet
}

if (-not $SkipImageBuild) {
    Invoke-Checked -Name 'Application image build' -Action {
        & $docker compose @composePrefix build --quiet
    }
}

$images = @(& $docker compose @composePrefix config --images)
if ($LASTEXITCODE -ne 0 -or $images.Count -eq 0) {
    throw 'Unable to obtain the Compose image list.'
}

if (-not (Get-Command docker -ErrorAction Stop)) {
    throw 'Docker CLI is required.'
}

foreach ($image in ($images | Where-Object { $_ -and $_.Trim() -and $_.Trim() -notmatch '^(postgres|rabbitmq)(:|$)' } | Sort-Object -Unique)) {
    $localReference = "local://$($image.Trim())"
    Invoke-Checked -Name "Docker Scout HIGH/CRITICAL scan: $($image.Trim())" -Action {
        & $docker scout cves --only-severity critical,high --exit-code $localReference
    }
}

Write-Host 'Security scan passed: no NuGet/npm production or repository-built application-image HIGH/CRITICAL findings.'
