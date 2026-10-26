using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents.Specifications
{
    // The code must already be normalized (upper case).
    public sealed class DocumentTypeByCodeSpecification : Specification<DocumentType>
    {
        public DocumentTypeByCodeSpecification(string code)
        {
            SetCriteria(t => t.Code == code);
        }
    }
}
