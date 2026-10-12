using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Students
{
    public static class StudentMapper
    {
        public static StudentSummary ToSummary(Student student) => new(student.Id, student.StudentNumber, student.PersonProfileId, student.UserId, student.AcademicStatus, student.Profile?.AcademicStanding, student.AdmissionDate, student.ExpectedGraduationDate, student.GraduationDate, student.CurrentProgram?.AcademicProgramId, student.Profile?.CurrentStudyPlanVersionId, student.Profile?.CurrentAcademicLevelId);

        public static StudentDetail ToDetail(Student student)
            => new(
                ToSummary(student),
                student.Profile?.AdmissionAcademicYearId,
                [.. student.ProgramHistory
                    .OrderBy(h => h.StartDate)
                    .Select(h => new ProgramHistoryItem(
                        h.Id, h.AcademicProgramId, h.StudyPlanVersionId, h.AcademicLevelId,
                        h.StartDate, h.EndDate, h.Status, h.Reason))],
                [.. student.StatusHistory
                    .OrderBy(h => h.CreatedAt)
                    .Select(h => new StatusHistoryItem(
                        h.Id, h.OldStatus, h.NewStatus, h.EffectiveDate, h.Reason, h.ChangedByUserId, h.CreatedAt))]);
    }


}
