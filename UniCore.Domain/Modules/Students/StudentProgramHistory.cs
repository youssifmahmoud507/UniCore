namespace UniCore.Domain.Modules.Students
{
    // Append-only: a row is only ever closed, never edited or deleted.
    public sealed class StudentProgramHistory
    {
        private StudentProgramHistory()
        {
        }

        public Guid Id { get; private set; }
        public Guid StudentId { get; private set; }
        public Guid AcademicProgramId { get; private set; }
        public Guid StudyPlanVersionId { get; private set; }
        public Guid? AcademicLevelId { get; private set; }
        public DateOnly StartDate { get; private set; }
        public DateOnly? EndDate { get; private set; }
        public StudentProgramStatus Status { get; private set; }
        public string? Reason { get; private set; }

        public bool IsOpen => EndDate is null;

        internal static StudentProgramHistory Open(Guid studentId, Guid academicProgramId, Guid studyPlanVersionId, Guid? academicLevelId, DateOnly startDate, string? reason)
            => new()
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                AcademicProgramId = academicProgramId,
                StudyPlanVersionId = studyPlanVersionId,
                AcademicLevelId = academicLevelId,
                StartDate = startDate,
                Status = StudentProgramStatus.Active,
                Reason = reason
            };

        internal void Close(DateOnly endDate, StudentProgramStatus status)
        {
            EndDate = endDate;
            Status = status;
        }
    }
}
