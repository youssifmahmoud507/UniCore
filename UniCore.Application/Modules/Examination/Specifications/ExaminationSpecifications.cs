using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Examination;

namespace UniCore.Application.Modules.Examination.Specifications
{
    // ---------- sessions ----------

    public sealed class ExamSessionByIdSpecification : Specification<ExamSession>
    {
        public ExamSessionByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(s => s.Id == id);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    public sealed class ExamSessionByNameSpecification : Specification<ExamSession>
    {
        public ExamSessionByNameSpecification(Guid semesterId, string name) => SetCriteria(s => s.AcademicSemesterId == semesterId && s.Name == name);
    }

    public sealed class ExamSessionsListSpecification : Specification<ExamSession>
    {
        public ExamSessionsListSpecification(PaginationQuery query, Guid? semesterId, ExamSessionStatus? status)
        {
            SetCriteria(s => (semesterId == null || s.AcademicSemesterId == semesterId) && (status == null || s.Status == status));
            ApplyOrderByDescending(s => s.StartDate);
            ApplyPaging(query);
        }
    }

    public sealed class ExamSessionsCountSpecification : Specification<ExamSession>
    {
        public ExamSessionsCountSpecification(Guid? semesterId, ExamSessionStatus? status)
            => SetCriteria(s => (semesterId == null || s.AcademicSemesterId == semesterId) && (status == null || s.Status == status));
    }

    // ---------- exams ----------

    public sealed class ExamByIdSpecification : Specification<Exam>
    {
        public ExamByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(e => e.Id == id);
            AddInclude("Schedule");
            AddInclude("Rooms");
            AddInclude("Invigilators");

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    // A section has one active exam of each type (a filtered unique index keeps it so).
    public sealed class ActiveExamOfSectionSpecification : Specification<Exam>
    {
        public ActiveExamOfSectionSpecification(Guid sectionId, ExamType type)
            => SetCriteria(e => e.CourseSectionId == sectionId && e.ExamType == type && e.Status != ExamStatus.Cancelled);
    }

    // The other exams of a day that have a time (the conflict check compares them with one exam).
    public sealed class ScheduledExamsOnDateSpecification : Specification<Exam>
    {
        public ScheduledExamsOnDateSpecification(DateOnly date, Guid excludeExamId)
        {
            SetCriteria(e => e.Id != excludeExamId && e.Status != ExamStatus.Cancelled && e.Schedule != null && e.Schedule.ExamDate == date);
            AddInclude("Schedule");
            AddInclude("Rooms");
            AddInclude("Invigilators");
        }
    }

    public sealed class ExamsListSpecification : Specification<Exam>
    {
        public ExamsListSpecification(PaginationQuery query, Guid? sessionId, Guid? sectionId, ExamStatus? status)
        {
            SetCriteria(e => (sessionId == null || e.ExamSessionId == sessionId) && (sectionId == null || e.CourseSectionId == sectionId) && (status == null || e.Status == status));
            AddInclude("Schedule");
            AddInclude("Rooms");
            AddInclude("Invigilators");
            ApplyOrderByDescending(e => e.CreatedAt);
            ApplyPaging(query);
        }
    }

    public sealed class ExamsCountSpecification : Specification<Exam>
    {
        public ExamsCountSpecification(Guid? sessionId, Guid? sectionId, ExamStatus? status)
            => SetCriteria(e => (sessionId == null || e.ExamSessionId == sessionId) && (sectionId == null || e.CourseSectionId == sectionId) && (status == null || e.Status == status));
    }
}
