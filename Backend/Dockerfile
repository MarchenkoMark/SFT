# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.101 AS restore
WORKDIR /src

COPY global.json ./
COPY Directory.Build.props ./
COPY src/SnakeForTwo.Api/SnakeForTwo.Api.csproj src/SnakeForTwo.Api/
COPY src/SnakeForTwo.Contracts/SnakeForTwo.Contracts.csproj src/SnakeForTwo.Contracts/
COPY src/SnakeForTwo.Game.Application/SnakeForTwo.Game.Application.csproj src/SnakeForTwo.Game.Application/
COPY src/SnakeForTwo.Game.Domain/SnakeForTwo.Game.Domain.csproj src/SnakeForTwo.Game.Domain/
COPY src/SnakeForTwo.Infrastructure/SnakeForTwo.Infrastructure.csproj src/SnakeForTwo.Infrastructure/
RUN dotnet restore src/SnakeForTwo.Api/SnakeForTwo.Api.csproj

FROM restore AS publish
COPY src/ src/
RUN dotnet publish src/SnakeForTwo.Api/SnakeForTwo.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/snakefortwo/data-protection-keys \
    && chown -R "$APP_UID:$APP_UID" /var/lib/snakefortwo

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080
COPY --from=publish /app/publish .

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

USER $APP_UID
ENTRYPOINT ["dotnet", "SnakeForTwo.Api.dll"]
