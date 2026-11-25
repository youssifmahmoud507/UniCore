using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Grading;

namespace UniCore.Application.Modules.Grading.Specifications
{
    public sealed class GradeChangeByIdSpecification : Specification<GradeChangeRequest>
    {
        public GradeChangeByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(r => r.Id == id);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    public sealed class GradeChangesOfGradeSpecification : Specification<GradeChangeRequest>
    {
        public GradeChangesOfGradeSpecification(Guid gradeId)
        {
            SetCriteria(r => r.GradeId == gradeId);
            ApplyOrderByDescending(r => r.RequestedAt);
        }
    }

    public sealed class PendingGradeChangeOfGradeSpecification : Specification<GradeChangeRequest>
    {
        public PendingGradeChangeOfGradeSpecification(Guid gradeId)
        {
            SetCriteria(r => r.GradeId == gradeId && r.Status == GradeChangeRequestStatus.Pending);
        }
    }
}
