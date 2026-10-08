# Authorization

Status: Draft v0.1. Owner: Developer 2. Developer 1 supplies ownership data (sections taught, faculty and department membership).

## 1. Layers

Every protected operation passes these checks in order:

1. **Authentication**: valid access token (`identity.md`). Failure returns `Unauthorized` (401).
2. **Role**: coarse grouping of permissions.
3. **Permission**: a code such as `Grades.Publish`.
4. **Scope**: where the permission applies (global, faculty, department, section).
5. **Resource ownership**: the caller owns the record (a student's own grades, an instructor's assigned section).

Failures at steps 2 to 5 return `Forbidden` (403). `[Authorize(Roles = ...)]` alone is never enough. Authorization failures are Results, never exceptions.

## 2. Roles

| Role | Scope | Typical access |
|---|---|---|
| SuperAdmin | Global | everything, role and permission management |
| Dean | Faculty | the faculty's departments, programs, courses, reports, approvals |
| DepartmentHead | Department | the department's courses, sections, instructors, approvals |
| Instructor | Assigned sections | attendance and grades for sections they teach |
| Student | Own records | own profile, registration, grades, invoices, requests |
| Registrar, FinanceOfficer, AdmissionsOfficer (proposed) | Global | operational staff for their module |

Roles are seeded. Permissions are assigned to roles in `RolePermission`.

## 3. Permissions

- Code constants in `UniCore.Domain/Modules/Identity/Permissions.cs`, format `Module.Action`, stored in `Permission` (`Code` unique, `Name`, `Description`, `Module`).
- Seeded from the constants. Each PR lists the permissions it adds.

Starter catalog (proposed):

| Module | Codes |
|---|---|
| Identity | `Users.Read`, `Users.Manage`, `Roles.Manage`, `Permissions.Manage` |
| University | `University.Read`, `University.Manage` |
| People | `People.Read`, `People.Manage` |
| Students | `Students.Read`, `Students.Manage`, `Students.ChangeStatus`, `Students.ChangeProgram` |
| Admissions | `Admissions.Read`, `Admissions.Review`, `Admissions.Decide` |
| Courses | `Courses.Read`, `Courses.Manage`, `Curriculum.Manage` |
| Registration | `Registration.Read`, `Registration.Register`, `Registration.Manage` |
| Attendance | `Attendance.Read`, `Attendance.Record`, `Attendance.ReviewExcuse` |
| Exams | `Exams.Read`, `Exams.Manage` |
| Grading | `Grades.Read`, `Grades.Enter`, `Grades.Review`, `Grades.Approve`, `Grades.Publish`, `GradeChanges.Request`, `GradeChanges.Approve` |
| Finance | `Finance.Read`, `Invoices.Manage`, `Payments.Record`, `Refunds.Approve`, `Holds.Manage` |
| Services | `Requests.Submit`, `Requests.Read`, `Requests.Process` |
| Documents | `Documents.Read`, `Documents.Upload`, `Documents.Manage` |
| Audit | `Audit.Read` |
| Platform | `Settings.Manage`, `Reports.Read` |

## 4. Scope

`ScopeAssignment`: `RoleId`, `UserId`, `ScopeType` (Global, Faculty, Department, Section), `ScopeId`, `CreatedAt`. Example: Dean at a faculty, DepartmentHead at a department.

- Scopes are hierarchical: Global covers everything, Faculty covers its departments and their programs, courses and sections, Department covers its programs, courses and sections.
- Instructor scope is derived from active `InstructorAssignment` rows (owned by Developer 1), not stored manually.
- `IScopeResolver` answers: "is resource R inside the caller's scope?". It needs, from Developer 1, a lookup from a resource to its faculty, department and section (for example a course section to its department and faculty).

## 5. Evaluation

For a request that needs permission P on resource R:

1. Not authenticated: `Unauthorized`.
2. Caller does not hold P through any of their roles: `Forbidden`.
3. Resolve the roles that grant P, then check that at least one of those role assignments has a scope covering R. Global covers all.
4. If the resource is personal (a student's grade or invoice), also require ownership unless a staff scope covers it.
5. All checks pass: allowed.

Implementation: policy-based authorization with `PermissionRequirement` and a resource-aware handler using `ICurrentUser` and `IScopeResolver`. Handlers return Results; the API maps them to 401/403.

## 6. Ownership rules

| Resource | Who |
|---|---|
| Student data, registration, grades, invoices, requests | the student (own), or staff with permission and scope |
| Section attendance, grades | instructors with an active `InstructorAssignment` on that section |
| Department resources | DepartmentHead of that department, Dean of its faculty |
| Documents | owner, or users with a valid `DocumentAccess`, or staff with `Documents.Manage` |

## 7. Management

- APIs for roles, role-permission assignment, scope assignment, user-role assignment.
- Guard rails: the last SuperAdmin cannot be removed; users cannot raise their own privileges; a role cannot be deleted while assigned.
- Every change is audited (`PERMISSION_GRANTED`, `PERMISSION_REVOKED`, `ROLE_ASSIGNED`, `ROLE_REMOVED`, `SCOPE_ASSIGNED`, `SCOPE_REMOVED`) with actor, target and before/after values.
- A permission or role change revokes the affected user's refresh tokens so it applies on the next login.

## 8. Caching

Permission sets are cached per user in Redis (Week 22) with a short expiry and explicit invalidation on any role, permission or scope change. If Redis fails, fall back to the database. Cache is never the source of truth.

## 9. Error codes

`FORBIDDEN_PERMISSION_MISSING`, `FORBIDDEN_OUT_OF_SCOPE`, `FORBIDDEN_NOT_OWNER`, `ROLE_NOT_FOUND`, `ROLE_IN_USE`, `LAST_SUPERADMIN`, `PERMISSION_NOT_FOUND`, `SCOPE_INVALID`.

## 10. Tests

Role without permission, permission without scope, scope hierarchy (dean to department to section), ownership, instructor on an unassigned section, student reading someone else's data, role management guard rails, audit of changes.