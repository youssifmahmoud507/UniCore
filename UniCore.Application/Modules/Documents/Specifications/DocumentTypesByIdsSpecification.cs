using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents.Specifications
{
    public sealed class DocumentTypesByIdsSpecification : Specification<DocumentType>
    {
        public DocumentTypesByIdsSpecification(IReadOnlyCollection<Guid> ids)
        {
            var list = ids.ToList();
            SetCriteria(t => list.Contains(t.Id));
        }
    }
}
