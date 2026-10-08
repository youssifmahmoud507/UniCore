# Platform Services

Status: Draft v0.1. Covers Student Services, Notifications, Documents, Audit, and the platform infrastructure (Outbox, Idempotency, caching, background jobs, observability). Values marked (proposed) need confirmation.

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
- Producers call `INotificationPublisher` with a notification type and a target (user or group). Types and templates are defined by the producing module (for example `GradePublished`, `AttendanceWarningIssued`, `RequestStatusChanged`, `AnnouncementPublished`, `PaymentRecorded`).
- Per-user preferences decide channels. Some types are mandatory (security events such as password changed) and ignore preferences.
- Delivery has a status (Pending, Sent, Failed) and retries with backoff (proposed: 3 attempts). Failures are Results; one failed channel does not block the others.
- Real-time: `NotificationHub` (SignalR), authenticated, one group per user. The hub only pushes; the database stays the source of truth.
- `AcademicHub` (Developer 1) carries registration, grade and announcement events.
- Emails and notifications go through the Outbox once it exists (Week 21).

## 3. Documents

Entities: `Document` (DocumentTypeId, OwnerUserId?, FileName, StorageKey, ContentType, SizeBytes, Status, CreatedAt), `DocumentType` (Code, Name, IsSensitive), `DocumentVersion` (DocumentId, VersionNumber, StorageKey, SizeBytes, CreatedAt, CreatedByUserId), `DocumentAccess` (DocumentId, UserId, AccessType, GrantedAt, ExpiresAt?).

- Files are stored outside `wwwroot` through `IFileStorage` (`LocalFileStorage` locally). Upload security rules are in `security.md`.
- Versions are append-only: a new upload creates a new version, never overwrites.
- Access: owner, users with a valid (unexpired) `DocumentAccess`, or staff with `Documents.Manage`. Sensitive types (`IsSensitive`) require an explicit grant.
- Every read and download of a sensitive document is audited.
- `IDocumentService` is the contract other modules use (admission requirement files, attendance excuses, generated certificates and transcripts).
- Generated documents (transcript PDFs) use a free PDF library and are stored through the same service.

## 4. Audit

`AuditLog`: `ActorUserId?`, `Action`, `EntityName`, `EntityId?`, `OldValues?`, `NewValues?`, `Timestamp`, `IpAddress?`, `UserAgent?`, `CorrelationId`.

- Append-only: no update or delete through the application.
- Must cover: grade changes, payments and refunds, student status and program changes, registration and enrollment, document access, permission and role changes, and authentication events.
- Old and new values are stored as JSON with sensitive fields masked (passwords, tokens, OTPs are never stored).
- Written through `IAuditWriter`. Until the Outbox exists (Week 21) calls are direct; afterwards they are event-driven from domain events, with the same interface.
- Read access requires `Audit.Read`. Retention (proposed): keep indefinitely, archive after 2 years.

## 5. Outbox

- Aggregates raise domain events; they are saved as `OutboxMessage` (EventType, Payload, OccurredAt, ProcessedAt?, RetryCount, Error?) in the same transaction as the business change.
- A Hangfire job processes pending messages and invokes handlers (audit, notifications, SignalR, integrations).
- Retries with a limit (proposed: 5), error capture, and a poison-message state after the limit.
- Handlers are idempotent because delivery is at least once.
- Never do save, email, SignalR and audit synchronously in one service method.

## 6. Idempotency

- `IdempotencyRecord` (Key, UserId?, RequestHash, ResponseStatus, ResponseBody?, CreatedAt, ExpiresAt), unique on (Key, UserId).
- Middleware/filter reads the `Idempotency-Key` header, stores the response, and replays it for the same key and payload (header `Idempotency-Replayed: true`). A different payload with the same key returns `409`.
- Applies to: register course, record payment, submit request, publish grade. Expiry (proposed): 24 hours. Cleanup by a job.

## 7. Caching (Redis)

- Cache-aside with expiry and explicit invalidation, for reference data, academic configuration, catalogues, lookups, permission sets, and frequently read read-models.
- Never the source of truth for registration, payments, grades or balances.
- Redis failures become Results and fall back to the database. `ICacheService` hides the implementation.

## 8. Background jobs (Hangfire, SQL Server storage)

- Recurring jobs: OutboxProcessing, NotificationDelivery, ExpiredRequests, FinancialHoldEvaluation, OTP cleanup, expired refresh-token cleanup, idempotency cleanup. Academic jobs (attendance warnings, status recalculation, graduation eligibility, expired admissions) belong to Developer 1.
- Every job is idempotent, returns Results internally, and logs with a CorrelationId. The dashboard is secured (`security.md`).

## 9. Observability

- Serilog (console first, Seq optional) with the CorrelationId on every log, audit entry and response.
- OpenTelemetry for HTTP duration and error rate, DB latency, background jobs, and SignalR connections.
- Health checks: `/health/live` (process up) and `/health/ready` (database, Redis, SMTP reachable).
- The global exception middleware logs unexpected exceptions with the CorrelationId and returns a 500 ProblemDetails.

## 10. Settings

`SystemSetting` (Key, Value, ValueType, Description?, IsSensitive, UpdatedAt, UpdatedBy?): default page size, maximum registration credits, attendance threshold, password policy, hold thresholds, feature flags. Changes are audited. Sensitive values are never returned by APIs.