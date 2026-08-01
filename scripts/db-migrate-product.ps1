$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot '..\src\Services\ProductService\MicroShop.ProductService\MicroShop.ProductService.csproj'
dotnet ef database update --project $project --startup-project $project
