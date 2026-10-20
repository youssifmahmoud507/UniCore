using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Admissions;

namespace UniCore.Application.Modules.Admissions.Specifications
{
    // One application with its requirements and decisions, tracked so the handler can change it.
    public sealed class ApplicationWithDetailsSpecification : Specification<AdmissionApplication>
    {
        public ApplicationWithDetailsSpecification(Guid id, bool tracking)
        {
            SetCriteria(a => a.Id == id);
            AddInclude(a => a.Requirements);
            AddInclude(a => a.Decisions);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    // The slot rule: one application per (applicant, program, academic year) that is not finished without admission.
    public sealed class ActiveApplicationSpecification : Specification<AdmissionApplication>
    {
        public ActiveApplicationSpecification(Guid applicantId, Guid programId, Guid academicYearId)
        {
            // Written out (not array.Contains) so the same expression runs in EF and in the in-memory test fake.
            // Keep it identical to AdmissionApplication.FinishedWithoutAdmission.
            SetCriteria(a => a.ApplicantId == applicantId && a.AcademicProgramId == programId
                && a.AcademicYearId == academicYearId
                && a.Status != AdmissionApplicationStatus.Rejected
                && a.Status != AdmissionApplicationStatus.Withdrawn
                && a.Status != AdmissionApplicationStatus.Expired);
        }
    }

    // Open applications whose deadline has passed, for the expiry job. Oldest deadline first, one batch at a time.
    public sealed class OverdueApplicationsSpecification : Specification<AdmissionApplication>
    {
        public OverdueApplicationsSpecification(DateTimeOffset now, int batchSize)
        {
            SetCriteria(a => a.Deadline < now
                && (a.Status == AdmissionApplicationStatus.Draft || a.Status == AdmissionApplicationStatus.Submitted
                    || a.Status == AdmissionApplicationStatus.UnderReview || a.Status == AdmissionApplicationStatus.ConditionallyAccepted));
            ApplyOrderBy(a => a.Deadline);
            UseTracking();
            ApplyPaging(new PaginationQuery(1, batchSize));
        }
    }

    public sealed class ApplicationsListSpecification : Specification<AdmissionApplication>
    {
        public static readonly string[] SortFields = ["createdAt", "submittedAt", "deadline"];

        public ApplicationsListSpecification(PaginationQuery query, AdmissionApplicationStatus? status, Guid? programId, Guid? academicYearId, Guid? applicantId, SortOption sort)
        {
            SetCriteria(a => (status == null || a.Status == status)
                && (programId == null || a.AcademicProgramId == programId)
                && (academicYearId == null || a.AcademicYearId == academicYearId)
                && (applicantId == null || a.ApplicantId == applicantId));

            switch (sort.Field)
            {
                case "submittedAt":
                    if (sort.Descending) { ApplyOrderByDescending(a => a.SubmittedAt!); } else { ApplyOrderBy(a => a.SubmittedAt!); }
                    break;
                case "deadline":
                    if (sort.Descending) { ApplyOrderByDescending(a => a.Deadline); } else { ApplyOrderBy(a => a.Deadline); }
                    break;
                default:
                    if (sort.Descending) { ApplyOrderByDescending(a => a.CreatedAt); } else { ApplyOrderBy(a => a.CreatedAt); }
                    break;
            }

            ApplyPaging(query);
        }
    }
}
