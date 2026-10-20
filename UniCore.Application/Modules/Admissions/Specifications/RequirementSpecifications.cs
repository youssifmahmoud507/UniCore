using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Admissions;

namespace UniCore.Application.Modules.Admissions.Specifications
{
    public sealed class RequirementByCodeSpecification : Specification<AdmissionRequirement>
    {
        public RequirementByCodeSpecification(Guid programId, string code)
        {
            SetCriteria(r => r.AcademicProgramId == programId && r.Code == code);
        }
    }

    public sealed class RequirementByIdSpecification : Specification<AdmissionRequirement>
    {
        public RequirementByIdSpecification(Guid id)
        {
            SetCriteria(r => r.Id == id);
            UseTracking();
        }
    }

    // The catalog of a program, optionally only the active entries.
    public sealed class RequirementsOfProgramSpecification : Specification<AdmissionRequirement>
    {
        public RequirementsOfProgramSpecification(Guid programId, bool activeOnly, bool tracking = false)
        {
            SetCriteria(r => r.AcademicProgramId == programId && (!activeOnly || r.IsActive));
            ApplyOrderBy(r => r.Code);

            if (tracking)
            {
                UseTracking();
            }
        }
    }
}
