using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // Tracked: the invoice issuer changes these charges. chargeIds narrows it to those charges.
    public sealed class PendingChargesForStudentSpecification : Specification<StudentCharge>
    {
        public PendingChargesForStudentSpecification(Guid studentId, Guid? academicSemesterId, IReadOnlyList<Guid>? chargeIds = null)
        {
            if (chargeIds is { Count: > 0 })
            {
                var ids = chargeIds.Distinct().ToArray();

                SetCriteria(c => c.StudentId == studentId && c.Status == ChargeStatus.Pending && (academicSemesterId == null || c.AcademicSemesterId == academicSemesterId) && ids.Contains(c.Id));
            }
            else
            {
                SetCriteria(c => c.StudentId == studentId && c.Status == ChargeStatus.Pending && (academicSemesterId == null || c.AcademicSemesterId == academicSemesterId));
            }

            ApplyOrderBy(c => c.CreatedAt);
            UseTracking();
        }
    }
}
