using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents.Specifications
{
    public sealed class DocumentAccessByIdSpecification : Specification<DocumentAccess>
    {
        public DocumentAccessByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(a => a.Id == id);

            if (tracking)
            {
                UseTracking();
            }
        }
    }
}
