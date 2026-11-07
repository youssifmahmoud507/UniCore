using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Registrations;

namespace UniCore.Application.Modules.Registrations.Specifications
{
    // A student has one registration per semester.
    public sealed class RegistrationByStudentSemesterSpecification : Specification<Registration>
    {
        public RegistrationByStudentSemesterSpecification(Guid studentId, Guid semesterId, bool tracking = false)
        {
            SetCriteria(r => r.StudentId == studentId && r.AcademicSemesterId == semesterId);
            AddInclude("Enrollments");

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    public sealed class RegistrationByIdSpecification : Specification<Registration>
    {
        public RegistrationByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(r => r.Id == id);
            AddInclude("Enrollments");

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    public sealed class RegistrationsOfStudentSpecification : Specification<Registration>
    {
        public static readonly string[] SortFields = ["createdAt", "status"];

        public RegistrationsOfStudentSpecification(PaginationQuery query, Guid studentId, Guid? semesterId, RegistrationStatus? status, SortOption sort)
        {
            SetCriteria(r => r.StudentId == studentId
                && (semesterId == null || r.AcademicSemesterId == semesterId)
                && (status == null || r.Status == status));
            AddInclude("Enrollments");

            if (sort.Field == "status")
            {
                if (sort.Descending) { ApplyOrderByDescending(r => r.Status); } else { ApplyOrderBy(r => r.Status); }
            }
            else
            {
                if (sort.Descending) { ApplyOrderByDescending(r => r.CreatedAt); } else { ApplyOrderBy(r => r.CreatedAt); }
            }

            ApplyPaging(query);
        }
    }

    // Counts the same rows as RegistrationsOfStudentSpecification without paging.
    public sealed class RegistrationsOfStudentCountSpecification : Specification<Registration>
    {
        public RegistrationsOfStudentCountSpecification(Guid studentId, Guid? semesterId, RegistrationStatus? status)
        {
            SetCriteria(r => r.StudentId == studentId
                && (semesterId == null || r.AcademicSemesterId == semesterId)
                && (status == null || r.Status == status));
        }
    }

    public sealed class RegistrationsWithSectionSpecification : Specification<Registration>
    {
        public RegistrationsWithSectionSpecification(Guid courseSectionId)
        {
            SetCriteria(r => r.Enrollments.Any(e => e.CourseSectionId == courseSectionId && (e.Status == EnrollmentStatus.Registered || e.Status == EnrollmentStatus.Completed)));
            AddInclude("Enrollments");
        }
    }

    // Registrations that still wait for the semester to close.
    public sealed class RegistrationsToCompleteSpecification : Specification<Registration>
    {
        public RegistrationsToCompleteSpecification(Guid semesterId)
        {
            SetCriteria(r => r.AcademicSemesterId == semesterId && (r.Status == RegistrationStatus.Draft || r.Status == RegistrationStatus.Submitted));
            AddInclude("Enrollments");
            UseTracking();
        }
    }

    public sealed class ClosedSemestersSpecification : Specification<AcademicSemester>
    {
        public ClosedSemestersSpecification()
        {
            SetCriteria(s => s.Status == SemesterStatus.Closed);
        }
    }
}
