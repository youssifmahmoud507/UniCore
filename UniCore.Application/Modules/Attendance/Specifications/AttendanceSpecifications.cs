using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Attendance;

namespace UniCore.Application.Modules.Attendance.Specifications
{
    // ---------- policies ----------

    public sealed class PolicyByIdSpecification : Specification<AttendancePolicy>
    {
        public PolicyByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(p => p.Id == id);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    // The active policy of a program or of a course (at most one each; a filtered unique index keeps it so).
    public sealed class ActivePolicyForTargetSpecification : Specification<AttendancePolicy>
    {
        public ActivePolicyForTargetSpecification(Guid? programId, Guid? courseId)
        {
            SetCriteria(p => p.IsActive && ((programId != null && p.AcademicProgramId == programId) || (courseId != null && p.CourseId == courseId)));
        }
    }

    public sealed class PoliciesListSpecification : Specification<AttendancePolicy>
    {
        public PoliciesListSpecification(PaginationQuery query, bool? activeOnly)
        {
            SetCriteria(p => activeOnly != true || p.IsActive);
            ApplyOrderByDescending(p => p.CreatedAt);
            ApplyPaging(query);
        }
    }

    public sealed class PoliciesCountSpecification : Specification<AttendancePolicy>
    {
        public PoliciesCountSpecification(bool? activeOnly) => SetCriteria(p => activeOnly != true || p.IsActive);
    }

    // ---------- sessions ----------

    public sealed class SessionByIdSpecification : Specification<AttendanceSession>
    {
        public SessionByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(s => s.Id == id);
            AddInclude("Records");

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    public sealed class SessionByRecordIdSpecification : Specification<AttendanceSession>
    {
        public SessionByRecordIdSpecification(Guid recordId, bool tracking = false)
        {
            SetCriteria(s => s.Records.Any(r => r.Id == recordId));
            AddInclude("Records");

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    public sealed class SessionBySlotSpecification : Specification<AttendanceSession>
    {
        public SessionBySlotSpecification(Guid sectionId, DateOnly date, TimeOnly start)
        {
            SetCriteria(s => s.CourseSectionId == sectionId && s.SessionDate == date && s.StartTime == start);
        }
    }

    // Every session of a section with its records (the percentage and the report read it).
    public sealed class SessionsOfSectionSpecification : Specification<AttendanceSession>
    {
        public SessionsOfSectionSpecification(Guid sectionId, bool closedOnly)
        {
            SetCriteria(s => s.CourseSectionId == sectionId && (!closedOnly || s.Status == AttendanceSessionStatus.Closed));
            AddInclude("Records");
            ApplyOrderBy(s => s.SessionDate);
        }
    }

    public sealed class SessionsListSpecification : Specification<AttendanceSession>
    {
        public SessionsListSpecification(PaginationQuery query, Guid sectionId, AttendanceSessionStatus? status)
        {
            SetCriteria(s => s.CourseSectionId == sectionId && (status == null || s.Status == status));
            AddInclude("Records");
            ApplyOrderByDescending(s => s.SessionDate);
            ApplyPaging(query);
        }
    }

    public sealed class SessionsCountSpecification : Specification<AttendanceSession>
    {
        public SessionsCountSpecification(Guid sectionId, AttendanceSessionStatus? status)
            => SetCriteria(s => s.CourseSectionId == sectionId && (status == null || s.Status == status));
    }

    // ---------- excuses ----------

    public sealed class ExcuseByIdSpecification : Specification<AttendanceExcuse>
    {
        public ExcuseByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(e => e.Id == id);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    // A pending or accepted excuse of the same absence (a rejected one may be followed by a new excuse).
    public sealed class ActiveExcuseOfRecordSpecification : Specification<AttendanceExcuse>
    {
        public ActiveExcuseOfRecordSpecification(Guid recordId)
            => SetCriteria(e => e.AttendanceRecordId == recordId && e.Status != ExcuseStatus.Rejected);
    }

    public sealed class ExcusesListSpecification : Specification<AttendanceExcuse>
    {
        public ExcusesListSpecification(PaginationQuery query, Guid? sectionId, Guid? studentId, ExcuseStatus? status)
        {
            SetCriteria(e => (sectionId == null || e.CourseSectionId == sectionId) && (studentId == null || e.StudentId == studentId) && (status == null || e.Status == status));
            ApplyOrderByDescending(e => e.SubmittedAt);
            ApplyPaging(query);
        }
    }

    public sealed class ExcusesCountSpecification : Specification<AttendanceExcuse>
    {
        public ExcusesCountSpecification(Guid? sectionId, Guid? studentId, ExcuseStatus? status)
            => SetCriteria(e => (sectionId == null || e.CourseSectionId == sectionId) && (studentId == null || e.StudentId == studentId) && (status == null || e.Status == status));
    }

    // ---------- warnings ----------

    public sealed class WarningsOfStudentSpecification : Specification<AttendanceWarning>
    {
        public WarningsOfStudentSpecification(Guid studentId, Guid? sectionId)
        {
            SetCriteria(w => w.StudentId == studentId && (sectionId == null || w.CourseSectionId == sectionId));
            ApplyOrderByDescending(w => w.IssuedAt);
        }
    }

    public sealed class WarningsOfSectionSpecification : Specification<AttendanceWarning>
    {
        public WarningsOfSectionSpecification(Guid sectionId)
        {
            SetCriteria(w => w.CourseSectionId == sectionId);
            ApplyOrderByDescending(w => w.IssuedAt);
        }
    }
}
