using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Admissions;

namespace UniCore.Application.Modules.Admissions.Specifications
{
    public sealed class ApplicantByEmailSpecification : Specification<Applicant>
    {
        public ApplicantByEmailSpecification(string email)
        {
            SetCriteria(a => a.Email == email);
        }
    }

    // The tracked applicant (loaded to be changed).
    public sealed class ApplicantByIdSpecification : Specification<Applicant>
    {
        public ApplicantByIdSpecification(Guid id)
        {
            SetCriteria(a => a.Id == id);
            UseTracking();
        }
    }

    public sealed class ApplicantsListSpecification : Specification<Applicant>
    {
        public static readonly string[] SortFields = ["name", "createdAt"];

        public ApplicantsListSpecification(PaginationQuery query, string? search, SortOption sort)
        {
            SetCriteria(a => search == null || a.FullName.Contains(search) || a.Email.StartsWith(search));

            if (sort.Field == "createdAt")
            {
                if (sort.Descending) { ApplyOrderByDescending(a => a.CreatedAt); } else { ApplyOrderBy(a => a.CreatedAt); }
            }
            else
            {
                if (sort.Descending) { ApplyOrderByDescending(a => a.FullName); } else { ApplyOrderBy(a => a.FullName); }
            }

            ApplyPaging(query);
        }
    }
}

namespace UniCore.Application.Modules.Admissions.Specifications
{
    // Another applicant (not this one) already uses the email.
    public sealed class ApplicantEmailTakenSpecification : Specification<Applicant>
    {
        public ApplicantEmailTakenSpecification(string email, Guid exceptId)
        {
            SetCriteria(a => a.Email == email && a.Id != exceptId);
        }
    }
}
