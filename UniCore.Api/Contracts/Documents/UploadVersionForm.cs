namespace UniCore.Api.Contracts.Documents
{
    // multipart/form-data: the field file.
    public sealed class UploadVersionForm
    {
        public IFormFile? File { get; set; }
    }

}
