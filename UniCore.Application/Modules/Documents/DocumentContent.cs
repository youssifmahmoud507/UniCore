namespace UniCore.Application.Modules.Documents
{
    // The caller disposes the stream.
    public sealed record DocumentContent(Stream Content, string FileName, string ContentType, long SizeBytes);


}
