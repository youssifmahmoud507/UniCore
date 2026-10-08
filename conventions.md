# UniCore Conventions

Status: Draft v0.1. Shared by both developers. Changes go through a PR reviewed by the partner.

## 1. Architecture

UniCore is a Modular Monolith built with Clean Architecture.

| Project | Responsibility | May reference |
|---|---|---|
| `UniCore.Domain` | Entities, aggregates, value objects, enums, domain events, `Result`/`Error` primitives | nothing |
| `UniCore.Application` | Use cases, handlers, validators, DTOs, abstractions (`IUnitOfWork`, `IClock`, `ICurrentUser`...) | Domain |
| `UniCore.Infrastructure` | EF Core, repositories, Identity, Redis, Hangfire, SMTP, file storage, SignalR | Application, Domain |
| `UniCore.Api` | Controllers, middleware, auth wiring, ProblemDetails mapping, `Program.cs` | Application, Infrastructure (composition root only) |

Rules (enforced by `UniCore.ArchitectureTests`):

- Domain references no other project and no EF Core / ASP.NET / Identity.
- Application references Domain only.
- Infrastructure never references Api.
- A module never uses another module's internal types or DbSets. Modules talk through Application-layer interfaces and domain events.
- Repositories exist only for aggregate roots.

### Modules

Identity, University, People, Admissions, Students, Curriculum, Courses, Registration, Scheduling, Attendance, Examination, Assessment, Grading, Academic (GPA/Transcript), Graduation, Finance, Services, Workflow, Communication, Notifications, Documents, Audit, Platform.

Inside each layer, code is organised by module folder, for example `UniCore.Domain/Modules/Finance/`. Namespaces mirror folder paths (`UniCore.Domain.Modules.Finance`). Cross-cutting code lives in `Common/`.

## 2. Hard rule: no exceptions thrown by our code

- Zero `throw` statements or `throw` expressions anywhere (domain, application, infrastructure, API, startup, tests helpers).
- Expected failures are returned as `Result` / `Result<T>` carrying an `Error`.
- Entities and value objects have private constructors and static factories returning `Result<T>`.
- Do not use APIs that throw as control flow: use `TryParse`, `TryGetValue`, `FirstOrDefault`/`SingleOrDefault` with explicit null handling. No `.First()`, `.Single()`, no `!` that hides a real null, no throwing guard helpers.
- Framework exceptions (`DbUpdateConcurrencyException`, `DbUpdateException`, timeouts, SMTP/IO/Redis failures) are caught at infrastructure boundaries and converted to Results. `catch` never rethrows.
- A global exception middleware exists only as a last-line safety net: it logs with the CorrelationId and returns a 500 ProblemDetails.
- `NoThrowTests` in `UniCore.ArchitectureTests` scans every `.cs` file and fails on any throw. Do not allow-list files.

## 3. Result and Error

```csharp
Result            // IsSuccess, IsFailure, Errors, Error (first error)
Result<T>         // adds Value (default on failure), TryGetValue, Match, Map, Bind
Error(Code, Description, ErrorType)
```

Consumers check `IsSuccess` (or use `TryGetValue` / `Match`) before using a value. `Result<T>.Value` never throws.

### ErrorType to HTTP status

| ErrorType | HTTP |
|---|---|
| Validation | 400 |
| Unauthorized, InvalidCredentials | 401 |
| Forbidden | 403 |
| NotFound | 404 |
| Conflict, Concurrency | 409 |
| BusinessRule | 422 |
| External | 503 |
| Failure | 500 |

### Error codes

- `UPPER_SNAKE_CASE`, unique across the solution, stable once released (clients depend on them).
- Defined as constants per module in `UniCore.Domain/Modules/<Module>/<Module>Errors.cs`:

```csharp
public static class IdentityErrors
{
    public static readonly Error AccountLockedOut =
        Error.Forbidden("ACCOUNT_LOCKED_OUT", "The account is temporarily locked.");
}
```

- Shared codes: `CONCURRENCY_CONFLICT`, `DUPLICATE_RESOURCE`, `EXTERNAL_SERVICE_UNAVAILABLE`, `GENERAL_*` defaults.
- Every PR lists the error codes it introduces.
- Error descriptions are safe to show to clients: no stack traces, SQL, secrets, or hints that reveal whether an account exists.

## 4. API conventions

- Base route: `/api/v1/<module-resource>`, plural kebab-case nouns (`/api/v1/academic-programs`). Version in the URL.
- JSON: camelCase, enums serialised as strings, dates ISO 8601 UTC, `DateOnly`/`TimeOnly` as `yyyy-MM-dd` / `HH:mm:ss`, money as numbers with 2 decimals.
- One central mapping: controllers return `result.ToActionResult(HttpContext)` (see `ResultExtensions`).
- Errors are RFC 7807 ProblemDetails (`application/problem+json`) with:
  - `type` = `urn:unicore:error:<ERROR_CODE>`, `title`, `status`, `detail`, `instance`
  - `errorCode`, `traceId`
  - `errors` dictionary for validation failures
- Standard verbs: `GET` list/detail, `POST` create/actions, `PUT` full update, `PATCH` partial, `DELETE` only where deletion is allowed. State changes that are not CRUD are `POST /{id}/activate`, `/deactivate`, `/submit`, etc.
- Created resources return `201` with `Location`.

### Pagination, sorting, filtering

- Query: `page` (default 1), `pageSize` (default 20, max 100), `sort` (comma separated, `-` prefix for descending, e.g. `sort=name,-createdAt`), `search` (free text), plus explicit filter fields (`isActive=true`, `facultyId=...`).
- Response envelope (`PagedResult<T>`):

```json
{ "items": [], "page": 1, "pageSize": 20, "totalCount": 0, "totalPages": 0, "hasPrevious": false, "hasNext": false }
```

- Only whitelisted sort fields are accepted. An unknown field returns a `Validation` error.

### Idempotency

- Header `Idempotency-Key` (GUID recommended). Required for: register course, record payment, submit request, publish grade.
- Same key and same payload replays the stored response. Same key with a different payload returns `409` (`IDEMPOTENCY_KEY_REUSED`). Missing key returns `400` (`IDEMPOTENCY_KEY_REQUIRED`).

### Docs

OpenAPI plus Scalar UI, enabled in Development only.

## 5. Naming

| Item | Convention | Example |
|---|---|---|
| Classes, records, enums | PascalCase | `CourseSection` |
| Interfaces | `I` prefix | `IFinancialEligibilityService` |
| Private fields | `_camelCase` | `_value` |
| Async methods | `Async` suffix | `SaveChangesAsync` |
| Commands / queries | `<Verb><Noun>Command` / `Get<Noun>Query` | `RegisterCourseCommand` |
| Handlers | `<Command>Handler` | `RegisterCourseCommandHandler` |
| Validators | `<Command>Validator` | `RegisterCourseCommandValidator` |
| Domain events | past tense | `EnrollmentCreated` |
| EF configurations | `<Entity>Configuration` | `InvoiceConfiguration` |
| Specifications | `<Description>Specification` | `ActiveCoursesSpecification` |
| Tests | `Method_condition_expectedResult` | `Otp_is_expired_after_10_minutes` |
| Permission codes | `Module.Action` | `Grades.Publish` |
| Error codes | `UPPER_SNAKE_CASE` | `COURSE_FULL` |

Folder per module in every project. One public type per file, file named after the type.

## 6. Contracts between developers

Contracts live in `UniCore.Application` and are agreed before implementation. Nobody edits the other's module internals; changes go through a PR to the owner.

Provided by Developer 2 (Platform): `Result`/`Error`, pagination, `ISpecification<T>`, `IRepository<T>`, `IUnitOfWork`, `IClock`, `ICurrentUser`, permission policies and `IScopeResolver`, `IFinancialEligibilityService`, `IStudentRepository`/`IStudentReadService`, `IPrerequisiteEvaluator`, `IAuditWriter`, `IIdempotencyService`, `INotificationPublisher`, `IDocumentService`, `ICacheService`, `IEmailSender`, `IWorkflowService`, `IFileStorage`.

Provided by Developer 1 (Academic): `IAcademicCalendarService`, `ICourseCatalogReadService`, `IStudyPlanReadService`, `IRegistrationQueryService`, `IEnrollmentReadService`, `IGradeReadService`, `IGpaCalculationService`, `ITranscriptService`, `IGraduationEligibilityService`, `IScheduleConflictService`, and the academic domain events (`StudentAdmitted`, `EnrollmentCreated`, `EnrollmentDropped`, `GradePublished`, `GraduationApproved`, `AttendanceWarningIssued`).

A contract change is a breaking change: open the PR, tag the partner, list consumers.

## 7. Database

- SQL Server. All primary keys are `Guid`.
- Timestamps `DateTimeOffset` (UTC), calendar dates `DateOnly`, clock times `TimeOnly`, money `decimal(18,2)`.
- One `IEntityTypeConfiguration<T>` per entity, in the module folder under Infrastructure. `AppDbContext` registers them by assembly scan.
- Enums are stored as strings, max length 50 (proposed: readable data and safe reordering).
- Schema per module (`identity`, `finance`, `workflow`...) and plural PascalCase table names (proposed).
- No cascade delete by default. History and version tables (`StudentProgramHistory`, `StudentStatusHistory`, `CourseVersion`, `StudyPlanVersion`, `DocumentVersion`, `AuditLog`) are append-only.
- `RowVersion` concurrency token on: Registration, Enrollment, CourseSection, Invoice, Payment, Grade, WorkflowInstance.
- Uniqueness is enforced in the database (unique indexes), not only in code. A violation becomes `DUPLICATE_RESOURCE`.
- Seed data lives in one seeder per module (roles, permissions, grade scales, terms, document types, workflow definitions, system settings).
- Migrations: one migration per PR. If two PRs conflict, the second merger deletes their migration, rebases on `develop`, and regenerates it. Migration names describe the change (`AddInvoiceTables`).
- Raw SQL only with parameters. Never concatenate user input.

## 8. Git

- Branches: `main` (releases), `develop` (integration), `feature/<module>-<topic>` (short-lived). Branch from `develop`, merge back to `develop`.
- Flow: feature branch, local tests green, PR, review by the partner, CI green, squash merge into `develop`, release merge into `main`.
- Commits follow Conventional Commits: `feat(identity): add refresh token rotation`, `fix(finance): ...`, `chore:`, `docs:`, `test:`, `refactor:`.
- PR description lists: entities touched, contracts changed, error codes, permissions, migrations, tests. Keep PRs small and focused.
- Never commit secrets, `.env` files, `bin/`, `obj/`, `.vs/`.

## 9. Testing

- xUnit with plain `Assert` for now. Unit tests for every business rule; integration tests for anything touching the database, authorization or concurrency.
- Integration tests use a real SQL Server container (Testcontainers), never SQLite or InMemory.
- Time-dependent code takes `IClock`; tests use `FakeClock`.
- Architecture tests: layer dependencies, module boundaries, no-throw scan.
- CI gates: build, unit tests, architecture tests, integration tests.

## 10. Definition of Done

Business rules, domain, application, validation, authorization, persistence, API, Result/Error handling, tests, audit where needed, and documentation are all in place and reviewed by the partner.