# Platform Services

Status: v1.0. Owner: Developer 2 (Yousif). Covers the platform infrastructure: Audit, Outbox, background jobs, caching, idempotency, reporting, notifications, documents, and student services.

## 1. Student Services

Entities (built by Developer 1, process owned by Developer 2's workflow engine): `CertificateRequest`, `TranscriptRequest`, `WithdrawalRequest`, `LeaveRequest` (FromDate, ToDate, Reason), `Complaint`, `Appeal`, `GradeAppeal` (GradeId).

- Every request has `StudentId`, `Status` (`RequestStatus`), `WorkflowInstanceId`, its own fields (type, purpose, reason, subject, description) and timestamps.
- Submitting a request starts a workflow instance (`workflow.md`) and requires `Idempotency-Key`.
- A student can have a blocking financial hold: services may be blocked per `finance.md`.
- Flow: request, workflow, approval, document (when a document is produced), notification.
- Students see only their own requests (`authorization.md`).

## 2. Notifications

Entities: `Notification` (UserId, Type, Title, Content, CreatedAt, ReadAt?), `NotificationPreference` (UserId, NotificationType, InAppEnabled, EmailEnabled), `NotificationDelivery` (NotificationId, Channel, Status, SentAt?, FailedAt?, ErrorMessage?).

- Channels: InApp and Email. The email path uses `IEmailSender` (MailKit, Mailpit locally).
- Producers call `INotificationPublisher` with a notification type and a target (user or group).
- Per-user preferences decide channels. Some types are mandatory (security events) and ignore preferences.
- Delivery has a status (Pending, Sent, Failed) and retries with backoff (3 attempts). Failures are Results; one failed channel does not block the others.
- Real-time: `NotificationHub` (SignalR), authenticated, one group per user. The hub only pushes; the database stays the source of truth.
- Emails and notifications go through the Outbox.

## 3. Documents

Entities: `Document` (DocumentTypeId, OwnerUserId?, FileName, StorageKey, ContentType, SizeBytes, Status, CreatedAt), `DocumentType` (Code, Name, IsSensitive), `DocumentVersion` (DocumentId, VersionNumber, StorageKey, SizeBytes, CreatedAt, CreatedByUserId), `DocumentAccess` (DocumentId, UserId, AccessType, GrantedAt, ExpiresAt?).

- Files are stored outside `wwwroot` through `IFileStorage` (`LocalFileStorage` locally). Upload security rules are in `security.md`.
- Versions are append-only: a new upload creates a new version, never overwrites.
- Access: owner, users with a valid (unexpired) `DocumentAccess`, or staff with `Documents.Manage`. Sensitive types (`IsSensitive`) require an explicit grant.
- Every read and download of a sensitive document is audited.
- `IDocumentService` is the contract other modules use.

## 4. Audit

`AuditLog`: `ActorUserId?`, `Action`, `EntityName`, `EntityId?`, `OldValues?`, `NewValues?`, `Timestamp`, `IpAddress?`, `UserAgent?`, `CorrelationId`.

- Append-only: no update or delete through the application.
- Written through `IAuditWriter` via `DbAuditWriter` (writes in a separate scope/DbContext so audit is preserved even if the main request fails or is cancelled).
- Read access requires `Audit.Read`. Query via `GET /api/v1/audit-logs` with paging and filters.

## 5. Outbox

- Aggregates raise domain events; they are saved as `OutboxMessage` (Pending/Processing/Processed/Failed) in the same transaction as the business change.
- `UnitOfWork.SaveChangesAsync` collects `DomainEvents` from tracked `AggregateRoot` entities, writes them as outbox rows in the same `SaveChanges`, and clears events only after success.
- `OutboxProcessor` does atomic claim via `ExecuteUpdateAsync` before running the handler (no double-processing). Claim expires after 5 minutes if the processor dies.
- **Retry policy:** 5 attempts with exponential backoff (30s → 2m → 8m → 32m, capped at 1h). Permanent failures (Validation/NotFound/Conflict/BusinessRule) go to poison immediately.
- Handlers implement `IDomainEventHandler<TEvent>` and must be **idempotent** (at-least-once delivery).
- Email payloads (may contain OTPs) are wiped (`{}`) once the message is processed or poisoned.
- `HangfireOutboxSignal` triggers immediate processing after a save with new messages.

## 6. Idempotency

- `IdempotencyRecord` (Key, RequestHash, ResponseStatus, ResponseBody, CreatedAt, ExpiresAt).
- `[Idempotent]` attribute on endpoints. The middleware reads the `Idempotency-Key` header.
- **Only 2xx responses are cached.** Errors are not cached (the client can retry with the same key).
- Same key and same payload replays the stored response (header `Idempotency-Replayed: true`). Same key with a different payload returns `409` (`IDEMPOTENCY_KEY_REUSED`). Missing key returns `400` (`IDEMPOTENCY_KEY_REQUIRED`).
- Expiry: 24 hours. Cleanup by a Hangfire recurring job.

## 7. Caching (Redis)

### Architecture

- **Cache-aside** via `ICacheService` (`DistributedCacheService`) backed by `IDistributedCache`.
- If `Redis:ConnectionString` is set: uses Redis (`StackExchange.Redis`). If empty: falls back to in-memory `MemoryDistributedCache`.
- `AbortOnConnectFail = false`: the API starts even when Redis is unreachable.
- Redis failures are caught and become Results — the API falls back to the database. Health check reports `Degraded`, not `Unhealthy`.

### Group versioning and invalidation

- `CacheInvalidationInterceptor` (EF `SaveChangesInterceptor`) watches saved entities and invalidates related cache groups automatically.
- Groups: `user-access:{userId}`, `permissions`, `roles`, and module-specific groups.
- `CacheInvalidationRules` maps entity types to their cache groups.

### Authorization caching

- `CachedUserAccessProvider` wraps `UserAccessProvider`. Per-request permission/scope loading hits Redis first.
- Any change to roles, permissions, or scope assignments invalidates the affected user's cache via the interceptor.
- Cache is **never** the source of truth for permissions. If Redis is down, every request loads from the database.

### Circuit breaker

- Redis connection options: `ConnectTimeout` and `CommandTimeout` from configuration (`Redis:ConnectTimeoutMs`, `Redis:CommandTimeoutMs`, defaults 5000/2000).
- Failed Redis operations are logged (type only, never values) and return a cache-miss Result.

## 8. Reporting

### Reports and dashboard

- `ReportDataSource` provides data for 7 built-in reports (enrollment stats, grade distributions, GPA analytics, attendance summary, financial summary, registration activity, program demographics).
- `GET /api/v1/reports/dashboard` returns a composite dashboard summary. Requires `Reports.Read`.
- `GET /api/v1/reports/{reportType}` returns individual report data with optional filters.

### Export

- `GET /api/v1/reports/{reportType}/export?format=csv|xlsx|pdf` exports report data.
- Requires `Reports.Export` permission.
- Exporters: `CsvReportExporter`, `XlsxReportExporter`, `PdfReportExporter`.
- Rate limited by the `export` policy (10 per minute per user).

### Registration

- Reports are registered via `AddReportingModule()` in `PlatformServiceCollectionExtensions`.
- `ReportingRegistrationTests` (architecture test) verifies all expected report types are wired.

## 9. Background jobs (Hangfire, SQL Server storage)

- Recurring jobs: OutboxProcessing (every minute), NotificationDelivery, ExpiredRequests, FinancialHoldEvaluation, OTP cleanup, expired refresh-token cleanup, idempotency cleanup.
- Academic jobs (attendance warnings, status recalculation, graduation eligibility, expired admissions) belong to Developer 1.
- Every job is idempotent, returns Results internally, and logs with a CorrelationId. The dashboard is secured (`security.md`).

## 10. Observability

- Serilog (console) with the CorrelationId on every log, audit entry and response.
- Health checks: `/health/live` (process up, no dependency checks) and `/health/ready` (database + Redis cache). Redis failure makes `/health/ready` return `Degraded` (not `Unhealthy`).
- The global exception middleware logs unexpected exceptions with the CorrelationId and returns a 500 ProblemDetails.