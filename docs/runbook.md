# UniCore Runbook

Operational procedures for the UniCore platform.

## SQL Server Backup

### Manual backup

```bash
docker exec unicore-sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -Q "
BACKUP DATABASE [UniCore] TO DISK = N'/var/opt/mssql/backup/UniCore_$(date +%Y%m%d_%H%M%S).bak'
WITH FORMAT, INIT, COMPRESSION, STATS = 10"
```

> The backup is stored inside the container's volume. For production, copy it out or mount an external volume.

### Restore from backup

```bash
docker exec unicore-sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -Q "
USE [master];
ALTER DATABASE [UniCore] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [UniCore] FROM DISK = N'/var/opt/mssql/backup/UniCore_YYYYMMDD_HHMMSS.bak'
WITH REPLACE, STATS = 10;
ALTER DATABASE [UniCore] SET MULTI_USER;"
```

## Secret Rotation

### JWT signing key

1. Set the new key in `.env` (or user-secrets): `JWT_SIGNING_KEY=<new-value>`
2. Restart the API: `docker compose restart api`
3. All existing access tokens become invalid immediately (15 min max). Users must re-login.

### Redis password

1. Update `REDIS_PASSWORD` in `.env`
2. Restart both Redis and API: `docker compose restart redis api`
3. During restart, the API falls back to database-only (Degraded health). No data loss.

### OTP pepper

1. Set the new pepper in `.env`: `PASSWORD_RESET_PEPPER=<new-value>`
2. Restart the API: `docker compose restart api`
3. All pending (unexpired) OTPs become invalid. Users must request a new OTP.

### SQL Server SA password

1. This is more complex because the existing container has the old password baked in:
   ```bash
   docker compose down
   # Update MSSQL_SA_PASSWORD in .env
   # If you need to keep data, do NOT use -v
   docker compose up -d sqlserver
   # Then change the password inside SQL Server:
   docker exec unicore-sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "<old>" -Q "ALTER LOGIN sa WITH PASSWORD = '<new>'"
   ```
2. Update the connection string in the API's environment/secrets.
3. Restart the API: `docker compose restart api`

## Redis Failure Handling

**What happens when Redis goes down:**

1. The `/health/ready` endpoint returns `Degraded` (HTTP 200 with status "Degraded"), not `Unhealthy`
2. The API continues to serve all requests normally
3. `CachedUserAccessProvider` falls back to loading permissions from the database on every request (slightly slower)
4. `ICacheService` operations return cache-miss Results — the application layer falls back to database queries
5. No data is lost (Redis is cache-only, never source of truth)

**When Redis comes back:**

1. The API reconnects automatically (`AbortOnConnectFail = false`)
2. The cache rebuilds organically as requests populate it
3. No manual intervention needed
4. `/health/ready` returns `Healthy` again

**To restart Redis:**

```bash
docker compose restart redis
```

## Hangfire Dashboard

The dashboard is disabled by default. To enable it temporarily for debugging:

```bash
# Set environment variable
export Hangfire__DashboardEnabled=true
docker compose restart api
```

Access at `http://localhost:8080/hangfire` (localhost only, read-only).

## Common Issues

### API won't start: "Options validation failed"

A required secret is missing or too short. Check that `Jwt:SigningKey` and `PasswordReset:Pepper` are set and at least 32 characters.

### Integration tests fail with "Could not find Docker"

Docker Desktop must be running. The tests use Testcontainers to start a SQL Server container.

### Migration conflict

If two developers have conflicting migrations:

```bash
dotnet ef migrations remove -p UniCore.Infrastructure -s UniCore.Api
# Rebase on master
dotnet ef migrations add <NewName> -p UniCore.Infrastructure -s UniCore.Api
```

