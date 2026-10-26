namespace UniCore.Application.Modules.Documents
{
    // The file to store. FileName and ContentType are what the client said: both are checked, neither is trusted.
    public sealed record DocumentUpload(string? FileName, string? ContentType, Stream Content);


}
