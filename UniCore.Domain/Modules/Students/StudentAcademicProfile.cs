namespace UniCore.Domain.Modules.Students
{
    // The student's current academic state. History lives in StudentProgramHistory.
    public sealed class StudentAcademicProfile
    {
        private StudentAcademicProfile()
        {
        }

        public Guid Id { get; private set; }
        public Guid StudentId { get; private set; }
        public Guid AdmissionAcademicYearId { get; private set; }
        public Guid? CurrentAcademicLevelId { get; private set; }
        public Guid? CurrentStudyPlanVersionId { get; private set; }
        public AcademicStanding AcademicStanding { get; private set; }

        internal static StudentAcademicProfile Create(
            Guid studentId, Guid admissionAcademicYearId, Guid? academicLevelId, Guid studyPlanVersionId)
            => new()
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                AdmissionAcademicYearId = admissionAcademicYearId,
                CurrentAcademicLevelId = academicLevelId,
                CurrentStudyPlanVersionId = studyPlanVersionId,
                AcademicStanding = AcademicStanding.GoodStanding
            };

        internal void SetPlacement(Guid? academicLevelId, Guid studyPlanVersionId)
        {
            CurrentAcademicLevelId = academicLevelId;
            CurrentStudyPlanVersionId = studyPlanVersionId;
        }

        internal void SetLevel(Guid academicLevelId) => CurrentAcademicLevelId = academicLevelId;

        internal void SetStanding(AcademicStanding standing) => AcademicStanding = standing;
    }
}
