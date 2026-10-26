using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents.Specifications
{
    public sealed class ActiveAccessForUserSpecification : Specification<DocumentAccess>
    {
        public ActiveAccessForUserSpecification(Guid documentId, Guid userId)
        {
            SetCriteria(a => a.DocumentId == documentId && a.UserId == userId && a.Status == DocumentAccessStatus.Active);
        }
    }
}
