# ============================================================
# UniCore API — Multi-stage Dockerfile
# ============================================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Copy solution and all csproj files first for layer caching
COPY UniCore.slnx ./
COPY UniCore.Domain/UniCore.Domain.csproj UniCore.Domain/
COPY UniCore.Application/UniCore.Application.csproj UniCore.Application/
COPY UniCore.Infrastructure/UniCore.Infrastructure.csproj UniCore.Infrastructure/
COPY UniCore.Api/UniCore.Api.csproj UniCore.Api/

# Restore (cached layer unless csproj files change)
RUN dotnet restore UniCore.Api/UniCore.Api.csproj

# Copy everything else and publish
COPY . .
RUN dotnet publish UniCore.Api/UniCore.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Non-root user
RUN groupadd -r appgroup && useradd -r -g appgroup -s /sbin/nologin appuser

# Install curl for the health check
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# No secrets in the image — all configuration via environment variables
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Health check: liveness probe
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -sf http://localhost:8080/health/live || exit 1

USER appuser
ENTRYPOINT ["dotnet", "UniCore.Api.dll"]

