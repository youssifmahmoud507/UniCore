# Developer 1 – review fixes (W9–W15)

| ID | Fix | Where |
|---|---|---|
| N-33 | `Assessment` cancel is rejected when grades exist (`ASSESSMENT_HAS_GRADES`). Submitting a grade sheet closes the scheme in the same save. A correction (and a returned grade) reads the scheme by the `AssessmentId` stored on the `Grade`. | `AssessmentHandler.CancelAsync`, `IAssessmentGradeUsage` (implemented by Grading), `IAssessmentLifecycle`, `IAssessmentReadService.GetAssessmentAsync`, `GradingEngine.LoadSchemeOfAssessmentAsync` |
| N-34 | Submit / Review / Approve / Publish / Lock skip grades whose enrollment is not on the roster (Registered or Completed). | `GradeEntryHandler.SubmitSectionAsync`, `GradeWorkflowHandler` (now takes `IEnrollmentReadService`) |
| N-35 | A semester cannot move to `Closed` while a grade of a still-enrolled student is not Published/Locked (`SEMESTER_HAS_UNPUBLISHED_GRADES`). | `ISemesterCloseGuard` (Calendar), `GradesSemesterCloseGuard` (Grading), `AcademicSemesterHandler.AdvanceStatusAsync` |
| N-36 | Exam session create/close need `Exams.Manage` with Global scope. An exam list without `sectionId` needs Global `Exams.Manage` (assessments already did this). | `ExaminationAccess.RequireUniversityWideAsync` |
| N-55 | `GpaCalculator.Summarize` passes the policy to the standing calculation. | `GpaCalculator.cs` |
| N-54 | `AcademicHistoryBuilder` loads semesters, years and terms in three queries instead of three per semester. | `TranscriptSpecifications.cs`, `AcademicHistoryBuilder.cs` |

No entity or table changed, so **no migration**.

Constructor changes (tests updated): `AssessmentHandler` (+`IAssessmentGradeUsage`), `GradeEntryHandler` (+`IAssessmentLifecycle`), `GradeWorkflowHandler` (+`IEnrollmentReadService`), `AcademicSemesterHandler` (+`IEnumerable<ISemesterCloseGuard>`).

Not in this patch (still open): N-37, N-38, N-40, N-41, N-44, N-47, N-56.
