using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Reporting
{
    public sealed record DocumentStat(string TypeCode, string TypeName, bool IsSensitive, DocumentStatus Status, int Count, long SizeBytes);




}
