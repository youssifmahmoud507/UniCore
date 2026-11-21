# Academic reports (Week 16)

Four reports on the Reporting platform (`IReportProvider`, same as `WorkflowRequestsReport`). They appear in `GET api/v1/reports` and run through `GET api/v1/reports/{code}/run` and `/export`.

| Code | Permission (plus Reports.Read) | Rows | Date filter column |
|---|---|---|---|
| ACADEMIC_ENROLLMENT_STATS | AcademicReports.Enrollment | department x status: enrollments, credits (+ Total) | Enrollment.RegisteredAt |
| ACADEMIC_GRADE_DISTRIBUTION | AcademicReports.Grades | department x letter: grades, share %, average % (+ Total) | Grade.PublishedAt |
| ACADEMIC_GPA_STATS | AcademicReports.Gpa | department: students, average/lowest/highest GPA, 4 bands (+ Total) | Grade.PublishedAt |
| ACADEMIC_ATTENDANCE_STATS | AcademicReports.Attendance | department: marks, present, late, excused, absent, rate % (+ Total) | AttendanceSession.SessionDate |

Parameters: `from`, `to` (both included, optional); the enrollment report also takes `status`.

## Rules
- Each report needs Reports.Read AND its own permission. Registrar, DepartmentHead and Dean hold all four; no other default role does. Existing databases are not re-seeded: grant them by hand.
- Scope (Y-18): every row sits in the department of its course. `IReportScopeResolver.ResolveAsync(<report permission>, departments)` decides which departments the user sees; a Faculty grant covers its departments, a Global grant everything. Nothing is shown to a user whose grant does not cover the department, and there is no row without a department.
- Grade distribution and GPA read Published and Locked grades only. A draft, submitted, reviewed or approved grade never appears.
- Everything is counted by the database (GROUP BY). The GPA report reads one small row per student (points x credits and credits) because a GPA needs the whole student; the department is the one of the student's latest program.
- Before a query runs, the rows it would scan are counted; more than 200,000 fails with `REPORT_SOURCE_TOO_LARGE` (narrow the dates). Other errors: `REPORT_DATE_RANGE_INVALID`, `REPORT_DATA_UNAVAILABLE`.
- Indexes for the date filters: Enrollments(RegisteredAt), Grades(Status, PublishedAt), AttendanceSessions(Status, SessionDate). Migration: `AddAcademicReportIndexes`.

## Definitions (read before using the numbers)
- GPA here = sum(grade points x credits) / sum(credits) over the student's final grades in the range. The REPEAT POLICY IS NOT APPLIED (the official GPA is the transcript's). Bands: below 2.0, 2.0 to 2.99, 3.0 to 3.49, 3.5 and above (an edge value is in the higher band).
- Attendance rate = (Present + Late + Excused) / all marks of CLOSED sessions. A late arrival beyond a course's own late threshold still counts as attended here (the per-student report applies the threshold).
- Enrollment date = when the seat was last taken (a dropped section that is registered again gets a new date).

## Where the data comes from
`IAcademicReportData` (Application) is implemented by `AcademicReportDataSource` (Infrastructure). It is a separate interface on purpose: `IReportDataSource` and its fakes belong to Dev 2 and are not touched. The four providers are registered in `AddReportingModule` (the architecture test requires it).

## Tests
Unit: `UniCore.UnitTests/Modules/Reporting/AcademicReportsTests.cs`. End to end, one or more per report with `RoleUsers`: `UniCore.IntegrationTests/EndToEnd/AcademicReportsApiTests.cs`.
