# ============================================================
# UniCore API — Multi-stage Dockerfile
# ============================================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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

# Stage 2: Migrations bundle (a self-contained executable that applies every EF Core migration).
# Built with `docker build --target migrations`; docker-compose runs it as the `migrate` service before the API.
# The environment values below exist only so the host can be built while the bundle is generated (nothing is
# connected to, and nothing from this stage reaches the runtime image).
FROM build AS migrations-build
RUN dotnet tool install --global dotnet-ef --version 10.0.15
ENV PATH="$PATH:/root/.dotnet/tools"
RUN RID="linux-x64"; if [ "$(uname -m)" = "aarch64" ]; then RID="linux-arm64"; fi; \
    ConnectionStrings__DefaultConnection="Server=build-only;Database=UniCore;User Id=sa;Password=build-only;TrustServerCertificate=True" \
    Jwt__SigningKey="build-only-build-only-build-only-build-only" \
    PasswordReset__Pepper="build-only-build-only-build-only-build-only" \
    dotnet ef migrations bundle \
        --project UniCore.Infrastructure/UniCore.Infrastructure.csproj \
        --startup-project UniCore.Api/UniCore.Api.csproj \
        --configuration Release \
        --self-contained -r "$RID" \
        --output /app/efbundle --force

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrations
WORKDIR /app
# No invariant globalization here: Microsoft.Data.SqlClient does not support it (the Debian image has ICU).
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp
COPY --from=migrations-build /app/efbundle ./efbundle
RUN chmod +x ./efbundle
# The connection string is passed by the caller: `--connection "<connection string>"`.
ENTRYPOINT ["./efbundle"]

# Stage 3: Runtime (must stay the last stage: `docker build .` builds it)
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

