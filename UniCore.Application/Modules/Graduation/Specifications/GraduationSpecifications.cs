using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Graduation;

namespace UniCore.Application.Modules.Graduation.Specifications
{
    public sealed class GraduationApplicationByIdSpecification : Specification<GraduationApplication>
    {
        public GraduationApplicationByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(a => a.Id == id);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    // A Submitted or Approved application: the student cannot apply again while one exists (the database enforces the same rule).
    public sealed class ActiveGraduationOfStudentSpecification : Specification<GraduationApplication>
    {
        public ActiveGraduationOfStudentSpecification(Guid studentId)
            => SetCriteria(a => a.StudentId == studentId && (a.Status == GraduationApplicationStatus.Submitted || a.Status == GraduationApplicationStatus.Approved));
    }

    public sealed class GraduationApplicationsOfStudentSpecification : Specification<GraduationApplication>
    {
        public GraduationApplicationsOfStudentSpecification(Guid studentId)
        {
            SetCriteria(a => a.StudentId == studentId);
            ApplyOrderByDescending(a => a.SubmittedAt);
        }
    }

    public sealed class GraduationApplicationsListSpecification : Specification<GraduationApplication>
    {
        public GraduationApplicationsListSpecification(PaginationQuery query, GraduationApplicationStatus? status)
        {
            SetCriteria(a => status == null || a.Status == status);
            ApplyOrderByDescending(a => a.SubmittedAt);
            ApplyPaging(query);
        }
    }

    public sealed class GraduationApplicationsCountSpecification : Specification<GraduationApplication>
    {
        public GraduationApplicationsCountSpecification(GraduationApplicationStatus? status)
            => SetCriteria(a => status == null || a.Status == status);
    }
}
