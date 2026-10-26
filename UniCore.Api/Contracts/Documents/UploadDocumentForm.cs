namespace UniCore.Api.Contracts.Documents
{
    // multipart/form-data: the fields typeCode and file.
    public sealed class UploadDocumentForm
    {
        public string? TypeCode { get; set; }

        public IFormFile? File { get; set; }
    }

}
