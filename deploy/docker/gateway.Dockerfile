ARG DOTNET_SDK_VERSION=10.0.302
ARG DOTNET_RUNTIME_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION} AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Gateway/MicroShop.Gateway/MicroShop.Gateway.csproj --locked-mode
RUN dotnet publish src/Gateway/MicroShop.Gateway/MicroShop.Gateway.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER 1654
LABEL org.opencontainers.image.title="MicroShop API Gateway" \
      org.opencontainers.image.description="YARP public API boundary for MicroShop services" \
      org.opencontainers.image.version="phase6"
ENTRYPOINT ["dotnet", "MicroShop.Gateway.dll"]

