using UniCore.Domain.Modules.Documents;

namespace UniCore.Api.Contracts.Documents
{
    public sealed record CreateDocumentTypeRequest(string? Code, string? Name, bool IsSensitive, List<DocumentFormat>? AllowedFormats);

}
