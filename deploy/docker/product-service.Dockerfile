ARG DOTNET_SDK_VERSION=10.0.302
ARG DOTNET_RUNTIME_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION} AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Services/ProductService/MicroShop.ProductService/MicroShop.ProductService.csproj --locked-mode
RUN dotnet publish src/Services/ProductService/MicroShop.ProductService/MicroShop.ProductService.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN apt-get update \
    && apt-get install --no-install-recommends --yes libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER 1654
LABEL org.opencontainers.image.title="MicroShop Product Service" \
      org.opencontainers.image.description="Product-owned catalog and inventory reservation API" \
      org.opencontainers.image.version="phase6"
ENTRYPOINT ["dotnet", "MicroShop.ProductService.dll"]

