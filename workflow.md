# Workflow Engine

Status: Draft v0.1. Owner of the engine: Developer 2. Developer 1 owns the request entities and their academic side effects. Seeded definitions below are proposed and must be confirmed.

## 1. Scope

A deliberately limited, step-based engine: linear steps, each approved by a role, optionally within a scope. It is not a general BPM engine.

Used only for: certificate requests, transcript requests, withdrawal, leave, complaints, appeals, grade appeals, and grade-change approval where applicable. Admissions uses it only if needed.

Not supported: parallel steps, conditions and branching, custom scripts, a visual designer, sub-workflows.

## 2. Entities

- `WorkflowDefinition`: `Code`, `Name`, `Version`, `IsActive`.
- `WorkflowStep`: `WorkflowDefinitionId`, `Name`, `Order`, `RequiredRole?`.
- `WorkflowInstance`: `WorkflowDefinitionId`, `CurrentStepId`, `Status`, `StartedAt`, `CompletedAt?`.
- `WorkflowAction`: `WorkflowInstanceId`, `WorkflowStepId`, `ActionType`, `PerformedByUserId`, `Comment?`, `PerformedAt`. Append-only log.
- `WorkflowApproval`: `WorkflowInstanceId`, `WorkflowStepId`, `ApproverUserId`, `Decision`, `Comment?`, `DecidedAt`.

Definitions are versioned. An instance is pinned to the version it started with. A definition that has instances is never edited; publish a new version instead.

## 3. Statuses and actions

Instance status: `InProgress`, `Completed` (approved at the last step), `Rejected`, `Cancelled`, `Returned` (sent back for correction).

Actions through `IWorkflowService`, each returning a `Result`:

| Action | Who | Effect |
|---|---|---|
| `Start(definitionCode, ...)` | the requesting student or staff | creates an instance at step 1 |
| `Approve(instanceId, comment?)` | approver of the current step | moves to the next step, or completes at the last step |
| `Reject(instanceId, comment)` | approver of the current step | ends as `Rejected`; a comment is mandatory |
| `Return(instanceId, comment)` | approver of the current step | status `Returned`; the requester can fix and resubmit, which restarts at step 1 |
| `Cancel(instanceId)` | the requester, or an admin | ends as `Cancelled` while not yet finished |

## 4. Rules

- Only a user holding the step's `RequiredRole`, with a scope covering the request (department or faculty of the student), can act on the current step.
- An approver cannot act on their own request (`WORKFLOW_SELF_APPROVAL`).
- Only the current step can be acted on. Finished instances accept no actions.
- One active instance per request.
- Every action writes a `WorkflowAction` and, for decisions, a `WorkflowApproval`. Both are audited.
- Concurrent decisions on the same instance are protected with `RowVersion`; the loser gets `CONCURRENCY_CONFLICT`.

## 5. Seeded definitions (proposed)

| Code | Steps |
|---|---|
| `CERTIFICATE_REQUEST` | Registrar review |
| `TRANSCRIPT_REQUEST` | Registrar review (financial eligibility checked automatically) |
| `WITHDRAWAL_REQUEST` | DepartmentHead, Dean, Registrar |
| `LEAVE_REQUEST` | DepartmentHead, Dean |
| `COMPLAINT` | DepartmentHead, Dean |
| `APPEAL` | DepartmentHead, Dean |
| `GRADE_APPEAL` | DepartmentHead, Dean |

## 6. Integration

- Each request entity (owned by Developer 1) stores `WorkflowInstanceId` and starts it through `IWorkflowService`.
- The engine raises `WorkflowCompleted`, `WorkflowRejected`, `WorkflowReturned`, `WorkflowCancelled` domain events through the Outbox. Request modules react to them to apply side effects (for example a completed withdrawal updates enrollments and student status, a completed grade appeal creates a `GradeChangeRequest`). The engine never calls academic code directly.
- Notifications: the next approvers on each step, the requester on every decision.
- Expired requests: the `ExpiredRequests` job cancels instances that exceed a configured age (proposed: 60 days idle) and returns a Result.

## 7. Error codes

`WORKFLOW_DEFINITION_NOT_FOUND`, `WORKFLOW_DEFINITION_INACTIVE`, `WORKFLOW_INSTANCE_NOT_FOUND`, `WORKFLOW_INVALID_STATE`, `WORKFLOW_NOT_CURRENT_APPROVER`, `WORKFLOW_SELF_APPROVAL`, `WORKFLOW_COMMENT_REQUIRED`, `WORKFLOW_ALREADY_ACTIVE`, `CONCURRENCY_CONFLICT`.

## 8. Permissions and audit

Permissions: `Requests.Submit`, `Requests.Read`, `Requests.Process`; approvals are controlled by the step role plus scope. Audit: instance started, each action and decision, cancellation, expiry.

## 9. Tests

Happy path through all steps, reject, return and resubmit, cancel, wrong role, out of scope, self approval, double approval concurrency, definition versioning, side effects only through events.