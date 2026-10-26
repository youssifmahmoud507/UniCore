using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents.Specifications
{
    public sealed class DocumentTypesSpecification : Specification<DocumentType>
    {
        public DocumentTypesSpecification()
        {
            SetCriteria(t => t.IsActive);
            ApplyOrderBy(t => t.Code);
        }
    }
}
