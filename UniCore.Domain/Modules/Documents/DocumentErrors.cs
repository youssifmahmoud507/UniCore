using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Documents
{
    public static class DocumentErrors
    {
        public static readonly Error NotFound = Error.NotFound("DOCUMENT_NOT_FOUND", "The document was not found.");
        public static readonly Error VersionNotFound = Error.NotFound("DOCUMENT_VERSION_NOT_FOUND", "The document version was not found.");
        public static readonly Error TypeNotFound = Error.NotFound("DOCUMENT_TYPE_NOT_FOUND", "The document type was not found.");
        public static readonly Error TypeInactive = Error.BusinessRule("DOCUMENT_TYPE_INACTIVE", "The document type is not active.");
        public static readonly Error TypeDataInvalid = Error.Validation("DOCUMENT_TYPE_DATA_INVALID", "The document type data is invalid.");
        public static readonly Error TypeCodeExists = Error.Conflict("DOCUMENT_TYPE_CODE_EXISTS", "A document type with this code already exists.");
        public static readonly Error DataInvalid = Error.Validation("DOCUMENT_DATA_INVALID", "The document data is invalid: a file with a valid name and extension is required.");
        public static readonly Error FileEmpty = Error.Validation("DOCUMENT_FILE_EMPTY", "The file is empty.");
        public static readonly Error FileTooLarge = Error.Validation("DOCUMENT_FILE_TOO_LARGE", "The file is larger than the allowed size.");
        public static readonly Error FormatNotAllowed = Error.Validation("DOCUMENT_FORMAT_NOT_ALLOWED", "This file format is not allowed for this document type.");
        public static readonly Error ContentMismatch = Error.Validation("DOCUMENT_CONTENT_MISMATCH", "The content of the file does not match its extension or declared type.");
        public static readonly Error ScanRejected = Error.Validation("DOCUMENT_SCAN_REJECTED", "The file was rejected by the security scan.");
        public static readonly Error Deleted = Error.BusinessRule("DOCUMENT_DELETED", "The document has been deleted.");
        public static readonly Error StorageUnavailable = Error.External("DOCUMENT_STORAGE_UNAVAILABLE", "The file storage is not available right now.");
        public static readonly Error FileMissing = Error.Failure("DOCUMENT_FILE_MISSING", "The stored file could not be found.");
        public static readonly Error AccessNotFound = Error.NotFound("DOCUMENT_ACCESS_NOT_FOUND", "The document access was not found.");
        public static readonly Error AccessInvalid = Error.Validation("DOCUMENT_ACCESS_INVALID", "The document access data is invalid.");
        public static readonly Error AccessExists = Error.Conflict("DOCUMENT_ACCESS_EXISTS", "This person already has access to the document.");
        public static readonly Error AccessNotActive = Error.BusinessRule("DOCUMENT_ACCESS_NOT_ACTIVE", "The document access is not active.");
    }
}
