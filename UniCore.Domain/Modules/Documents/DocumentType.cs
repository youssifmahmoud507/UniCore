using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Documents
{
    // A kind of document (admission paper, excuse certificate, transcript...). It says which formats are allowed
    // and whether documents of this kind are sensitive: a sensitive document is readable only by its owner and by
    // people who were given access to it, and every read is audited.
    public sealed class DocumentType : AggregateRoot
    {
        public const int MinCodeLength = 2;
        public const int MaxCodeLength = 30;
        public const int MaxNameLength = 150;

        private DocumentType()
        {
        }

        public string Code { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public bool IsSensitive { get; private set; }
        public DocumentFormat AllowedFormats { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }

        // Letters A-Z, digits and underscore, upper case. Null means invalid.
        public static string? NormalizeCode(string? code)
        {
            var clean = code?.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(clean) || clean.Length < MinCodeLength || clean.Length > MaxCodeLength)
            {
                return null;
            }

            return clean.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_') ? clean : null;
        }

        public static Result<DocumentType> Create(string? code, string? name, bool isSensitive, DocumentFormat allowedFormats, DateTimeOffset now)
        {
            var cleanCode = NormalizeCode(code);
            var cleanName = name?.Trim();

            if (cleanCode is null || string.IsNullOrEmpty(cleanName) || cleanName.Length > MaxNameLength || allowedFormats == DocumentFormat.None || (allowedFormats & ~DocumentFormat.All) != DocumentFormat.None)
            {
                return DocumentErrors.TypeDataInvalid;
            }

            return new DocumentType
            {
                Id = Guid.NewGuid(),
                Code = cleanCode,
                Name = cleanName,
                IsSensitive = isSensitive,
                AllowedFormats = allowedFormats,
                IsActive = true,
                CreatedAt = now
            };
        }

        public bool Allows(DocumentFormat format) => format != DocumentFormat.None && (AllowedFormats & format) == format;
    }



}
