# Security

Status: v1.0. Owner: Developer 2 (Yousif). Reflects the implemented state as of Week 25.

## 1. Principles

Defence in depth, least privilege, secure by default, fail closed. Validation happens in layers (input validation, domain rules, authorization, database constraints). Validation libraries are never the only line of defence.

## 2. Authentication and tokens

Defined in `identity.md`: short-lived JWT (15 min), hashed rotating refresh tokens with reuse detection, lockout (5 attempts / 15 min), OTP password reset with generic responses. Authorization is defined in `authorization.md`.

## 3. Rate limiting

Built-in ASP.NET Core rate limiter. Exceeding a limit returns `429` with `Retry-After`.

| Policy | Limit |
|---|---|
| `login` | 5 per minute per IP |
| `forgot-password` | 10 per hour per IP |
| `verify-otp` | 5 per 10 minutes per IP |
| `reset-password` | 5 per 10 minutes per IP |
| `refresh` | 20 per minute per IP |
| `upload` | 5 per minute per authenticated user |
| `export` | 10 per minute per authenticated user |
| general API | 100 per minute per authenticated user |

Rate limit responses must not reveal whether an account exists.

## 4. HTTP security

- **HTTPS** everywhere, HTTPS redirection, **HSTS** in production (365 days, includeSubDomains, no preload).
- **CORS**: explicit allowed origins from `Cors:AllowedOrigins` configuration, never `*` with credentials. Allowed methods: GET, POST, PUT, PATCH, DELETE. Allowed headers: Authorization, Content-Type, Accept, Idempotency-Key, X-Correlation-Id, X-Requested-With, X-SignalR-User-Agent. Exposed headers: Retry-After, X-Correlation-Id, Content-Disposition, Idempotency-Replayed. Preflight max age configurable. Fail closed: no origins configured means nobody is allowed.
- **Trusted proxies**: `Proxy:TrustedNetworks` configuration (IP addresses or CIDR ranges). `X-Forwarded-For` and `X-Forwarded-Proto` are trusted only from these networks. `ForwardLimit = 1`.
- **Headers** on every response (via `SecurityHeadersMiddleware`):
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: no-referrer`
  - `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`
  - `Cache-Control: no-store` on auth endpoints
- **Kestrel limits**: `AddServerHeader = false` (no `Server: Kestrel` header), default max request body size 1 MB (`DefaultMaxRequestBodyBytes`). Upload endpoints raise the limit for their own actions.
- Scalar/OpenAPI UI only in Development.

### Hangfire dashboard

- **Disabled by default.** Set `Hangfire:DashboardEnabled=true` to enable (development only).
- When enabled: **read-only** and accessible **from localhost only** (`IsLocalRequest` filter).
- No write operations, no job triggers, no retries from the dashboard.

## 5. Secrets

- Never in the repository: no passwords, keys, connection strings with credentials, SMTP credentials, or JWT signing keys.
- Local development: `dotnet user-secrets` and environment variables. Docker: environment variables from an untracked `.env` file. CI: GitHub Actions secrets.
- `.env` and secret files are in `.gitignore`.
- A missing or invalid secret makes the app refuse to start (the Options validation failure is raised by the framework at startup). This covers: `Jwt:SigningKey` (≥32 bytes), `PasswordReset:Pepper` (≥32 bytes), `Redis:ConnectionString` (validated format), `Smtp:*` (host, port, from address), `Cors:AllowedOrigins` (valid origin format).

## 6. Logging and data

- Never log passwords, tokens (access or refresh), OTPs, reset tokens, hashes, or full card/ID numbers. Never return password, token or OTP hashes in any response.
- Log with the CorrelationId on every entry. Mask emails and personal data in logs.
- Personal data (national ID, contact details, documents) is returned only to authorised callers and accessed through audited paths.
- Error responses carry no stack traces, SQL, or internal paths.

## 7. Input and persistence

- Validation on every command (handlers validate; FluentValidation where added). Domain factories validate again.
- EF Core parameterised queries only. No SQL string concatenation.
- Request size limits (Kestrel default 1 MB). Strict model binding: unknown sort or filter fields are rejected.

## 8. File uploads

- Allow-list of extensions and MIME types per document type, and verify magic bytes against the claimed type.
- Maximum size per type (default: 10 MB).
- Random storage keys; never use the user's file name for storage; sanitise the display name.
- Stored outside `wwwroot` behind `IFileStorage`; served only through an authorised endpoint, with `Content-Disposition: attachment` and `nosniff`.
- Antivirus hook point (`IFileScanner`) invoked before the file is accepted; a no-op implementation locally.
- Every access to a sensitive document is audited.

## 9. Dependencies and build

- `dotnet list package --vulnerable --include-transitive` in CI as a required gate — High/Critical vulnerabilities fail the pipeline.
- Dependabot alerts enabled on the repository.
- Pin versions in the project files. Review new packages before adding them.
- Architecture tests enforce layer rules and the no-throw rule in CI as required gates.

## 10. Authorization review checklist

Every endpoint must declare: authentication requirement, permission, scope, ownership rule, audit events. A PR adding an endpoint without them is rejected.

## 11. Incident basics

Revoke all refresh tokens on suspected compromise (supported by design), rotate the JWT signing key and OTP pepper (supported by configuration), and use the audit log with the CorrelationId to trace activity.