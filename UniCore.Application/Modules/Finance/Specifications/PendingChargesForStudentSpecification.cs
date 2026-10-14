using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // Tracked: the invoice handler changes these charges.
    public sealed class PendingChargesForStudentSpecification : Specification<StudentCharge>
    {
        public PendingChargesForStudentSpecification(Guid studentId, Guid? academicSemesterId)
        {
            SetCriteria(c => c.StudentId == studentId && c.Status == ChargeStatus.Pending && (academicSemesterId == null || c.AcademicSemesterId == academicSemesterId));

            ApplyOrderBy(c => c.CreatedAt);
            UseTracking();
        }
    }

}
