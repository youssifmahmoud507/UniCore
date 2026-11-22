# UniCore — University Management System

Enterprise university management backend: .NET 10, EF Core, SQL Server, Clean Architecture.

## Quick Start

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (running)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

### 1. Clone and configure

```bash
git clone https://github.com/youssifmahmoud507/UniCore.git
cd UniCore
cp .env.example .env
```

Edit `.env` and set **real values** for all variables (especially passwords and keys).

### 2. Start infrastructure

```bash
docker compose up -d sqlserver redis mailpit
```

Wait until SQL Server is healthy:

```bash
docker compose ps   # sqlserver should show "healthy"
```

### 3. Apply database migrations

```bash
dotnet ef database update -p UniCore.Infrastructure -s UniCore.Api
```

### 4. Configure user secrets (local development)

```bash
cd UniCore.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=UniCore;User Id=sa;Password=<your .env password>;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:SigningKey" "<at least 32 characters>"
dotnet user-secrets set "PasswordReset:Pepper" "<at least 32 characters>"
dotnet user-secrets set "SeedAdmin:Email" "admin@unicore.local"
dotnet user-secrets set "SeedAdmin:UserName" "superadmin"
dotnet user-secrets set "SeedAdmin:Password" "<strong password>"
cd ..
```

### 5. Run the API

```bash
dotnet run --project UniCore.Api
```

The API starts at `https://localhost:7094` and `http://localhost:5087`. Scalar UI at `/scalar` (Development only).

### Alternative: Run everything in Docker

```bash
docker compose up -d
```

This builds and starts the API container along with SQL Server, Redis, and Mailpit. The API is available at `http://localhost:8080` (set `API_PORT` in `.env` if that port is taken, for example `API_PORT=8081`).

> **Note:** `docker compose up` runs the `migrate` service first (an EF Core migrations bundle built by the `migrations` stage of the Dockerfile). It creates or updates the database and exits; the API starts only after it succeeded. You do not need to run `dotnet ef database update` for the compose stack. Look at its result with `docker compose logs migrate`.

After the stack is up, `./scripts/smoke.sh` (Git Bash, WSL or Linux) checks the real container: health, the seeded admin login and a read of each main module. It reads `.env` and `API_PORT`; use `BASE_URL=http://host:port` for another address.

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `MSSQL_SA_PASSWORD` | ✅ | SQL Server SA password |
| `REDIS_PASSWORD` | ✅ | Redis password |
| `JWT_SIGNING_KEY` | ✅ | JWT signing key (≥32 characters) |
| `PASSWORD_RESET_PEPPER` | ✅ | OTP HMAC pepper (≥32 characters) |
| `SEED_ADMIN_EMAIL` | Optional | Initial SuperAdmin email |
| `SEED_ADMIN_USERNAME` | Optional | Initial SuperAdmin username |
| `SEED_ADMIN_PASSWORD` | Optional | Initial SuperAdmin password |
| `ConnectionStrings__DefaultConnection` | Docker | Full SQL Server connection string |
| `Redis__ConnectionString` | Docker | Redis connection string |
| `Cors__AllowedOrigins__0` | Optional | Allowed CORS origin |
| `Smtp__Host` | Optional | SMTP host (default: localhost) |
| `Smtp__Port` | Optional | SMTP port (default: 1025) |
| `Hangfire__DashboardEnabled` | Optional | Enable Hangfire dashboard (default: false) |
| `RateLimiting__Disabled` | Dev only | Disable rate limiting (Development env only) |

## Running Tests

```bash
# All tests (Docker must be running for integration tests)
dotnet test

# Unit tests only
dotnet test UniCore.UnitTests

# Architecture tests (includes no-throw scan)
dotnet test UniCore.ArchitectureTests

# Integration tests (needs Docker for SQL Server container via Testcontainers)
dotnet test UniCore.IntegrationTests
```

## Health Endpoints

| Endpoint | Purpose | Checks |
|---|---|---|
| `/health/live` | Liveness probe | Process is up (no dependency checks) |
| `/health/ready` | Readiness probe | Database + Redis cache (Redis down = Degraded, not Unhealthy) |

## Adding a New Module

1. Add domain entities in `UniCore.Domain/Modules/<YourModule>/`
2. Add application handlers/DTOs in `UniCore.Application/Modules/<YourModule>/`
3. Add EF configuration in `UniCore.Infrastructure/Persistence/Configurations/`
4. Create a registration extension: `UniCore.Infrastructure/Modules/<YourModule>/<YourModule>ServiceCollectionExtensions.cs`
5. Call it from your team's composition point:
   - **Platform team:** Add to `PlatformServiceCollectionExtensions.AddPlatformModules()`
   - **Academic team:** Add to `DependencyInjection.AddInfrastructure()` (single line)
6. Add controllers in `UniCore.Api/Controllers/`
7. Add a migration: `dotnet ef migrations add <Name> -p UniCore.Infrastructure -s UniCore.Api`
8. Add tests in `UniCore.UnitTests/Modules/<YourModule>/` and `UniCore.IntegrationTests/`

> **Important:** Do not modify `Program.cs` or `DependencyInjection.cs` unless absolutely necessary (single line max). These files are protected by CODEOWNERS and require platform team review.

## Adding a New Report

1. Implement `IReportProvider` in `UniCore.Application/Modules/Reporting/`
2. Register it in `AddReportingModule()` in `UniCore.Infrastructure/Modules/Reporting/ReportingServiceCollectionExtensions.cs`
3. The `ReportingRegistrationTests` architecture test will fail if a report class exists but is not registered

## Services (Docker Compose)

| Service | Port | Purpose |
|---|---|---|
| `sqlserver` | 1433 | SQL Server 2022 Developer |
| `redis` | 6379 (localhost only) | Cache (not source of truth) |
| `mailpit` | 1025 (SMTP), 8025 (UI) | Local email testing |
| `api` | 8080 (`API_PORT`) | UniCore API |

## Project Structure

```
UniCore.Domain/            → Entities, value objects, enums, Result/Error (no dependencies)
UniCore.Application/       → Use cases, handlers, DTOs, interfaces (depends on Domain)
UniCore.Infrastructure/    → EF Core, repositories, Identity, Redis, Hangfire, SMTP
UniCore.Api/               → Controllers, middleware, Program.cs
UniCore.UnitTests/         → Unit tests
UniCore.IntegrationTests/  → Integration tests (Testcontainers + WebApplicationFactory)
UniCore.ArchitectureTests/ → Layer rules, no-throw scan, security checks
```
