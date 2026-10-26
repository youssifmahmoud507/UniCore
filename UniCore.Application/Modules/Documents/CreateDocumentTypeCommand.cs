using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents
{
    public sealed record CreateDocumentTypeCommand(string? Code, string? Name, bool IsSensitive, IReadOnlyList<DocumentFormat>? AllowedFormats);


}
