namespace UniCore.Domain.Modules.Documents
{
    // What was stored for one version of a document. The StorageKey is a random name made by the system:
    // a path or file name from a user never reaches the disk.
    public sealed record DocumentVersionData(string FileName, string ContentType, long SizeBytes, string StorageKey, string Checksum, Guid? CreatedByUserId);



}
