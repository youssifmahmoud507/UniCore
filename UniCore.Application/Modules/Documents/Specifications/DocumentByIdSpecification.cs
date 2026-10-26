using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents.Specifications
{
    public sealed class DocumentByIdSpecification : Specification<Document>
    {
        public DocumentByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(d => d.Id == id);
            AddInclude("Versions");

            if (tracking)
            {
                UseTracking();
            }
        }
    }
}
