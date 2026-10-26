using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents
{
    public sealed record DocumentTypeDto(Guid Id, string Code, string Name, bool IsSensitive, IReadOnlyList<DocumentFormat> AllowedFormats, bool IsActive);


}
