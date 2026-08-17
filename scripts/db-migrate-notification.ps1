$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot '..\src\Services\NotificationService\MicroShop.NotificationService\MicroShop.NotificationService.csproj'
dotnet ef database update --project $project --startup-project $project
