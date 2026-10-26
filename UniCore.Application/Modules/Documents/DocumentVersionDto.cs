namespace UniCore.Application.Modules.Documents
{
    public sealed record DocumentVersionDto(int VersionNumber, string FileName, string ContentType, long SizeBytes, string Checksum, Guid? CreatedByUserId, DateTimeOffset CreatedAt);


}
