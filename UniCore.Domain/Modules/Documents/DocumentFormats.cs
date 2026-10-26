namespace UniCore.Domain.Modules.Documents
{
    // What a file REALLY is is decided by its first bytes, never by its name or the type the client declares.
    public static class DocumentFormats
    {
        // Enough bytes to recognise every format below.
        public const int HeaderLength = 8;

        private static readonly byte[] PdfSignature = [0x25, 0x50, 0x44, 0x46, 0x2D];
        private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
        private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

        public static DocumentFormat FromExtension(string? extension)
        {
            var clean = extension?.Trim().TrimStart('.').ToLowerInvariant();

            return clean switch
            {
                "pdf" => DocumentFormat.Pdf,
                "png" => DocumentFormat.Png,
                "jpg" or "jpeg" => DocumentFormat.Jpeg,
                "docx" => DocumentFormat.Docx,
                _ => DocumentFormat.None
            };
        }

        // A .docx file is a zip archive, so a zip signature means Docx (the extension decides which zip-based kind).
        public static DocumentFormat Detect(ReadOnlySpan<byte> header)
        {
            if (header.StartsWith(PdfSignature))
            {
                return DocumentFormat.Pdf;
            }

            if (header.StartsWith(PngSignature))
            {
                return DocumentFormat.Png;
            }

            if (header.StartsWith(JpegSignature))
            {
                return DocumentFormat.Jpeg;
            }

            if (header.StartsWith(ZipSignature))
            {
                return DocumentFormat.Docx;
            }

            return DocumentFormat.None;
        }

        public static string ContentTypeOf(DocumentFormat format) => format switch
        {
            DocumentFormat.Pdf => "application/pdf",
            DocumentFormat.Png => "image/png",
            DocumentFormat.Jpeg => "image/jpeg",
            DocumentFormat.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };

        // The single formats inside a set (Images -> Png, Jpeg).
        public static IReadOnlyList<DocumentFormat> Split(DocumentFormat formats)
        {
            DocumentFormat[] all = [DocumentFormat.Pdf, DocumentFormat.Png, DocumentFormat.Jpeg, DocumentFormat.Docx];

            return [.. all.Where(format => (formats & format) != DocumentFormat.None)];
        }
    }



}
