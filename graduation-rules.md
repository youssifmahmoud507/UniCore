# Graduation (Week 16)

## Flow
1. The student asks for eligibility (`GET api/v1/graduation/students/{id}/eligibility`) and applies (`POST .../students/{id}/applications`).
2. Only one application per student can be active (Submitted or Approved). A handler check gives a clean `GRADUATION_ALREADY_APPLIED`; a filtered unique index `UX_GraduationApplications_OneActivePerStudent` closes the race.
3. The student may cancel while Submitted. After Cancelled or Rejected the student may apply again.
4. The registrar (Graduation.Decide, Global scope) approves or rejects. Approval checks, in order: permission, load, Global scope, state Submitted, financial hold (`FinancialHoldArea.Graduation`), eligibility again. Then in ONE save: Student becomes Graduated (GraduationDate = IAcademicClock.Today), the application is Approved with the frozen GPA/credits/standing, `GraduationApproved` is raised, and the audit row carries the registrar as actor.

## Eligibility rules
- Student is Active and has a current program (else `GRADUATION_STUDENT_NOT_ACTIVE`, nothing else is checked).
- Every Required course of the StudyPlanVersion the student FOLLOWS (not the latest) is passed: `GRADUATION_REQUIRED_COURSES_MISSING` (the description lists the course codes). A repeated course counts once (passed-course ids come from `IGpaCalculationService.GetPassedCourseIdsAsync`, which uses the Week 15 repeat policy).
- Earned credits >= `AcademicProgram.TotalRequiredCredits`: `GRADUATION_CREDITS_INSUFFICIENT`.
- Cumulative GPA >= `GraduationPolicy.MinGpa` (2.0, inclusive; no graded credits counts as below): `GRADUATION_GPA_BELOW_MINIMUM`.
- All unmet requirements are returned together, each with its own code.

## Permissions
| Permission | Student | DepartmentHead / Dean | Registrar |
|---|---|---|---|
| Graduation.Read | own | own department | yes (Global) |
| Graduation.Apply | own | | |
| Graduation.Decide | | | yes (Global) |

Existing databases are not re-seeded: add these grants to the existing roles by hand (or by SQL).

## Error codes
GRADUATION_STUDENT_NOT_FOUND, GRADUATION_APPLICATION_NOT_FOUND, GRADUATION_INVALID, GRADUATION_REASON_REQUIRED, GRADUATION_ALREADY_APPLIED, GRADUATION_INVALID_STATE, GRADUATION_NOT_ELIGIBLE, GRADUATION_ACTOR_REQUIRED, GRADUATION_STUDENT_NOT_ACTIVE, GRADUATION_REQUIRED_COURSES_MISSING, GRADUATION_CREDITS_INSUFFICIENT, GRADUATION_GPA_BELOW_MINIMUM, FINANCIAL_HOLD.

## Audit actions
GRADUATION_APPLIED, GRADUATION_CANCELLED, GRADUATION_APPROVED, GRADUATION_REJECTED.

## Event: GraduationApproved (payload PROPOSED, to agree with Dev 2)
ApplicationId, StudentId, AcademicProgramId, StudyPlanVersionId, CumulativeGpa, EarnedCredits, Standing, GraduationDate, ApprovedByUserId (+ EventId, OccurredAt).
Consumers: Notifications (Week 19), Documents/graduation certificate (Week 20), Outbox delivery (Week 21). Nobody consumes it yet.

## Contracts
- `IGraduationEligibilityService.CheckAsync(studentId)` returns `GraduationEligibility` (IsEligible, Unmet errors, MissingRequiredCourses).
- `IGpaCalculationService.GetPassedCourseIdsAsync` was added for this.

## Open decisions
- "Academic components" and per-type elective credit quotas are not checked: the study plan has no quotas yet.
- The Graduation module writes `Student` (status change) through `IAggregateRepository<Student>`; confirm with Dev 2 that this is acceptable instead of an `IStudentStatusService`.
- Migration: `AddGraduation`.
