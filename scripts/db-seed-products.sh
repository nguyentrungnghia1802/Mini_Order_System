#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$repo_root/src/Services/ProductService/MicroShop.ProductService/MicroShop.ProductService.csproj"
dotnet run --project "$project" -- --seed
