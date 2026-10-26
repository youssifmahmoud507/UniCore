using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents.Specifications
{
    // The active documents a person owns, newest first.
    public sealed class DocumentsOfOwnerSpecification : Specification<Document>
    {
        public DocumentsOfOwnerSpecification(Guid ownerUserId, PaginationQuery page)
        {
            SetCriteria(d => d.OwnerUserId == ownerUserId && d.Status == DocumentStatus.Active);
            AddInclude("Versions");
            ApplyOrderByDescending(d => d.CreatedAt);
            ApplyPaging(page);
        }
    }
}
