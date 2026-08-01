$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot '..\src\Services\OrderService\MicroShop.OrderService\MicroShop.OrderService.csproj'
dotnet ef database update --project $project --startup-project $project
