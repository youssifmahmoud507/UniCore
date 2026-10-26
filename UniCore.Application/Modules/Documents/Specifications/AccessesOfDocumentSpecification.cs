using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents.Specifications
{
    public sealed class AccessesOfDocumentSpecification : Specification<DocumentAccess>
    {
        public AccessesOfDocumentSpecification(Guid documentId)
        {
            SetCriteria(a => a.DocumentId == documentId);
            ApplyOrderByDescending(a => a.GrantedAt);
        }
    }
}
