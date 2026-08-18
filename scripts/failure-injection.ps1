[CmdletBinding()]
param(
    [ValidateSet('all', 'product-stopped', 'notification-stopped', 'rabbitmq-stopped', 'integration-tests')]
    [string]$Scenario = 'all',
    [string]$EnvFile = '.env.example',
    [string]$BaseUrl = 'http://localhost:8080'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$composeArguments = @('--env-file', $EnvFile, '-f', 'deploy/compose.yaml')
$httpClient = [System.Net.Http.HttpClient]::new()
$httpClient.Timeout = [TimeSpan]::FromSeconds(25)

function Invoke-Compose {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

    & docker compose @composeArguments @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Api {
    param(
        [Parameter(Mandatory = $true)][ValidateSet('GET', 'POST')][string]$Method,
        [Parameter(Mandatory = $true)][string]$Uri,
        [object]$Body
    )

    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::$Method,
        $Uri)
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 8 -Compress
        $request.Content = [System.Net.Http.StringContent]::new(
            $json,
            [System.Text.Encoding]::UTF8,
            'application/json')
    }

    try {
        $response = $httpClient.SendAsync($request).GetAwaiter().GetResult()
        $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Body = $responseBody
        }
    }
    catch {
        return [pscustomobject]@{
            StatusCode = 0
            Body = $_.Exception.Message
        }
    }
    finally {
        $request.Dispose()
    }
}

function Assert-Status {
    param(
        [Parameter(Mandatory = $true)][pscustomobject]$Response,
        [Parameter(Mandatory = $true)][int]$Expected,
        [Parameter(Mandatory = $true)][string]$Description
    )

    if ($Response.StatusCode -ne $Expected) {
        throw "$Description expected HTTP $Expected but received $($Response.StatusCode): $($Response.Body)"
    }
}

function Wait-Ready {
    param([int]$TimeoutSeconds = 60)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $response = Invoke-Api GET "$BaseUrl/health/ready"
        $productResponse = Invoke-Api GET "$BaseUrl/api/products?page=1&limit=1"
        if ($response.StatusCode -eq 200 -and $productResponse.StatusCode -eq 200) {
            return
        }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw "Gateway readiness did not recover within $TimeoutSeconds seconds."
}

function New-TestProduct {
    $name = "Failure Injection Product $(Get-Date -Format 'yyyyMMddHHmmssfff')"
    $response = Invoke-Api POST "$BaseUrl/api/products" @{
        name = $name
        description = 'Created by the non-destructive failure-injection harness.'
        unitPrice = 1000
        currency = 'VND'
        initialStock = 5
        isActive = $true
    }
    Assert-Status $response 201 "Create failure-injection Product"
    return ($response.Body | ConvertFrom-Json)
}

function New-TestOrder {
    param([Parameter(Mandatory = $true)][string]$ProductId)

    $response = Invoke-Api POST "$BaseUrl/api/orders" @{
        customerName = 'Failure Injection Runner'
        customerEmail = "failure-injection-$([Guid]::NewGuid())@example.com"
        items = @(@{ productId = $ProductId; quantity = 1 })
    }
    return $response
}

function Wait-Notification {
    param([Parameter(Mandatory = $true)][string]$OrderId, [int]$TimeoutSeconds = 45)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $response = Invoke-Api GET "$BaseUrl/api/notifications?orderId=$OrderId&page=1&limit=20"
        if ($response.StatusCode -eq 200) {
            $page = $response.Body | ConvertFrom-Json
            if ($page.items.Count -gt 0) {
                return
            }
        }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw "Notification for Order $OrderId did not arrive within $TimeoutSeconds seconds."
}

function Assert-NoNotificationYet {
    param([Parameter(Mandatory = $true)][string]$OrderId)

    Start-Sleep -Seconds 2
    $response = Invoke-Api GET "$BaseUrl/api/notifications?orderId=$OrderId&page=1&limit=20"
    if ($response.StatusCode -in @(502, 503, 504)) {
        return
    }
    Assert-Status $response 200 "Read Notification state for Order $OrderId"
    $page = $response.Body | ConvertFrom-Json
    if ($page.items.Count -ne 0) {
        throw "Notification for Order $OrderId arrived before the dependency was restored."
    }
}

function Run-ProductStopped {
    Write-Host '[product-stopped] stopping Product Service'
    $product = New-TestProduct
    Invoke-Compose stop product-service
    try {
        $response = New-TestOrder $product.id
        if ($response.StatusCode -notin @(502, 503, 504)) {
            throw "Product stopped scenario expected a dependency failure, received HTTP $($response.StatusCode): $($response.Body)"
        }
        Write-Host "[product-stopped] dependency failure observed for Product $($product.id)."
    }
    finally {
        Invoke-Compose start product-service
        Wait-Ready
    }
}

function Run-NotificationStopped {
    Write-Host '[notification-stopped] stopping Notification Service'
    $product = New-TestProduct
    Invoke-Compose stop notification-service
    try {
        $response = New-TestOrder $product.id
        Assert-Status $response 201 'Create Order while Notification Service is stopped'
        $order = $response.Body | ConvertFrom-Json
        Assert-NoNotificationYet $order.id
        Write-Host "[notification-stopped] confirmed Order $($order.id) remained durable without a Notification."
    }
    finally {
        Invoke-Compose start notification-service
        Wait-Ready
    }
    Wait-Notification $order.id
    Write-Host "[notification-stopped] Notification for Order $($order.id) arrived after recovery."
}

function Run-RabbitMqStopped {
    Write-Host '[rabbitmq-stopped] stopping RabbitMQ'
    $product = New-TestProduct
    Invoke-Compose stop rabbitmq
    try {
        $response = New-TestOrder $product.id
        Assert-Status $response 201 'Create confirmed Order while RabbitMQ is stopped'
        $order = $response.Body | ConvertFrom-Json
        Assert-NoNotificationYet $order.id
        Write-Host "[rabbitmq-stopped] confirmed Order $($order.id) remained durable while RabbitMQ was stopped."
    }
    finally {
        Invoke-Compose start rabbitmq
        Wait-Ready
    }
    Wait-Notification $order.id
    Write-Host "[rabbitmq-stopped] outbox recovery delivered Notification for Order $($order.id)."
}

function Run-IntegrationTests {
    $sdkDirectory = 'C:\WINDOWS\TEMP\microshop-dotnet-sdk-10.0.302'
    $dotnetCommand = 'dotnet'
    if (Test-Path (Join-Path $sdkDirectory 'dotnet.exe')) {
        $env:DOTNET_ROOT = $sdkDirectory
        $env:Path = "$sdkDirectory;$env:Path"
        $dotnetCommand = Join-Path $sdkDirectory 'dotnet.exe'
    }
    elseif (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "The pinned Windows SDK was not found at $sdkDirectory and dotnet is not available on PATH."
    }

    $testCases = @(
        @('tests/MicroShop.NotificationService.Tests/MicroShop.NotificationService.Tests.csproj', 'FullyQualifiedName~PublishesDuplicateEventWithOneDurableNotification|FullyQualifiedName~MovesUnsupportedMessageToErrorQueueAfterBoundedRetry'),
        @('tests/MicroShop.ProductService.Tests/MicroShop.ProductService.Tests.csproj', 'FullyQualifiedName~ConcurrentLastStockReservationsAllowOnlyOneSuccess'),
        @('tests/MicroShop.OrderService.Tests/MicroShop.OrderService.Tests.csproj', 'FullyQualifiedName~RabbitMqOutageLeavesConfirmedOrderDurableAndRecoveryDrainsOutbox')
    )

    foreach ($testCase in $testCases) {
        Write-Host "[integration-tests] running $($testCase[1])"
        & $dotnetCommand test $testCase[0] --configuration Release --no-restore --filter $testCase[1]
        if ($LASTEXITCODE -ne 0) {
            throw "Integration test filter failed: $($testCase[1])"
        }
    }
}

Push-Location $repositoryRoot
try {
    Invoke-Compose config --quiet
    Invoke-Compose up -d --wait
    Wait-Ready

    if ($Scenario -in @('all', 'product-stopped')) { Run-ProductStopped }
    if ($Scenario -in @('all', 'notification-stopped')) { Run-NotificationStopped }
    if ($Scenario -in @('all', 'rabbitmq-stopped')) { Run-RabbitMqStopped }
    if ($Scenario -in @('all', 'integration-tests')) { Run-IntegrationTests }

    Write-Host "Failure-injection scenario '$Scenario' completed. Containers and volumes were preserved."
}
finally {
    $httpClient.Dispose()
    Pop-Location
}
