using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents.Specifications
{
    public sealed class DocumentTypeByIdSpecification : Specification<DocumentType>
    {
        public DocumentTypeByIdSpecification(Guid id)
        {
            SetCriteria(t => t.Id == id);
        }
    }
}
