using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // Existing (not cancelled) charges of these students in this semester. Used to make generation idempotent.
    public sealed class ChargesForStudentsSemesterSpecification : Specification<StudentCharge>
    {
        public ChargesForStudentsSemesterSpecification(Guid academicSemesterId, Guid[] studentIds)
        {
            SetCriteria(c => c.AcademicSemesterId == academicSemesterId && c.Status != ChargeStatus.Cancelled && studentIds.Contains(c.StudentId));
        }
    }

}
