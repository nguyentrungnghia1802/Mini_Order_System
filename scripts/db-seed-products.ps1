$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot '..\src\Services\ProductService\MicroShop.ProductService\MicroShop.ProductService.csproj'
dotnet run --project $project -- --seed
