using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Modules.Documents;
using UniCore.Domain.Modules.Documents;

namespace UniCore.UnitTests.Modules.Documents
{
    public class DocumentDomainTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

        private static string Key() => Guid.NewGuid().ToString("N");

        private static string Sum() => new('a', 64);

        private static DocumentVersionData Data(string name = "cv.pdf", long size = 100, string? key = null, string? checksum = null)
            => new(name, "application/pdf", size, key ?? Key(), checksum ?? Sum(), Guid.NewGuid());

        private static Document NewDocument(Guid? owner = null)
        {
            var result = Document.Create(Guid.NewGuid(), owner, Data(), Now);
            Assert.True(result.TryGetValue(out var document));
            Assert.NotNull(document);
            return document;
        }

        // ---------- formats ----------

        [Fact]
        public void The_first_bytes_decide_the_format()
        {
            Assert.Equal(DocumentFormat.Pdf, DocumentFormats.Detect("%PDF-1.7"u8));
            Assert.Equal(DocumentFormat.Png, DocumentFormats.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));
            Assert.Equal(DocumentFormat.Jpeg, DocumentFormats.Detect([0xFF, 0xD8, 0xFF, 0xE0]));
            Assert.Equal(DocumentFormat.Docx, DocumentFormats.Detect([0x50, 0x4B, 0x03, 0x04, 1]));
            Assert.Equal(DocumentFormat.None, DocumentFormats.Detect([1, 2, 3]));
            Assert.Equal(DocumentFormat.None, DocumentFormats.Detect([]));
        }

        [Theory]
        [InlineData(".PDF", DocumentFormat.Pdf)]
        [InlineData("png", DocumentFormat.Png)]
        [InlineData("jpg", DocumentFormat.Jpeg)]
        [InlineData(".jpeg", DocumentFormat.Jpeg)]
        [InlineData("docx", DocumentFormat.Docx)]
        [InlineData("exe", DocumentFormat.None)]
        [InlineData("", DocumentFormat.None)]
        [InlineData(null, DocumentFormat.None)]
        public void An_extension_maps_to_a_format(string? extension, DocumentFormat expected)
        {
            Assert.Equal(expected, DocumentFormats.FromExtension(extension));
        }

        [Fact]
        public void A_set_of_formats_splits_into_single_ones()
        {
            Assert.Equal([DocumentFormat.Png, DocumentFormat.Jpeg], DocumentFormats.Split(DocumentFormat.Images));
            Assert.Equal(4, DocumentFormats.Split(DocumentFormat.All).Count);
        }

        // ---------- file names ----------

        [Theory]
        [InlineData("My CV.pdf", "My CV.pdf")]
        [InlineData("..\\..\\evil.pdf", "evil.pdf")]
        [InlineData("/etc/passwd.pdf", "passwd.pdf")]
        [InlineData("a<b>:c|d?.pdf", "abcd.pdf")]
        [InlineData("  spaced.png  ", "spaced.png")]
        public void A_file_name_loses_its_folders_and_special_characters(string raw, string expected)
        {
            Assert.Equal(expected, DocumentFileName.Sanitize(raw));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("noextension")]
        [InlineData("../..")]
        public void A_file_name_with_nothing_usable_is_refused(string? raw)
        {
            Assert.Null(DocumentFileName.Sanitize(raw));
        }

        [Fact]
        public void A_long_name_is_shortened_and_keeps_its_extension()
        {
            var name = DocumentFileName.Sanitize(new string('n', 300) + ".pdf");

            Assert.NotNull(name);
            Assert.Equal(DocumentFileName.MaxLength, name.Length);
            Assert.EndsWith(".pdf", name);
        }

        // ---------- types ----------

        [Fact]
        public void A_type_has_a_normalized_code_and_checks_formats()
        {
            var result = DocumentType.Create(" admission_doc ", "Admission", true, DocumentFormat.Pdf | DocumentFormat.Images, Now);

            Assert.True(result.TryGetValue(out var type));
            Assert.NotNull(type);
            Assert.Equal("ADMISSION_DOC", type.Code);
            Assert.True(type.IsSensitive);
            Assert.True(type.Allows(DocumentFormat.Pdf));
            Assert.True(type.Allows(DocumentFormat.Png));
            Assert.False(type.Allows(DocumentFormat.Docx));
            Assert.False(type.Allows(DocumentFormat.None));
        }

        [Fact]
        public void A_bad_type_is_refused()
        {
            Assert.Equal("DOCUMENT_TYPE_DATA_INVALID", DocumentType.Create("x", "Name", false, DocumentFormat.Pdf, Now).Errors[0].Code);
            Assert.Equal("DOCUMENT_TYPE_DATA_INVALID", DocumentType.Create("OK_CODE", " ", false, DocumentFormat.Pdf, Now).Errors[0].Code);
            Assert.Equal("DOCUMENT_TYPE_DATA_INVALID", DocumentType.Create("OK_CODE", "Name", false, DocumentFormat.None, Now).Errors[0].Code);
            Assert.Equal("DOCUMENT_TYPE_DATA_INVALID", DocumentType.Create("OK_CODE", "Name", false, (DocumentFormat)64, Now).Errors[0].Code);
        }

        // ---------- documents and versions ----------

        [Fact]
        public void A_new_document_has_version_one_as_its_current_version()
        {
            var owner = Guid.NewGuid();
            var document = NewDocument(owner);

            Assert.Equal(owner, document.OwnerUserId);
            Assert.Equal(DocumentStatus.Active, document.Status);
            Assert.Equal(1, document.CurrentVersionNumber);
            Assert.Equal("cv.pdf", document.FileName);
            Assert.Single(document.Versions);
            Assert.Equal(1, document.Current?.VersionNumber);
            Assert.Null(document.UpdatedAt);
        }

        [Fact]
        public void Bad_version_data_is_refused()
        {
            var type = Guid.NewGuid();

            Assert.Equal("DOCUMENT_DATA_INVALID", Document.Create(Guid.Empty, null, Data(), Now).Errors[0].Code);
            Assert.Equal("DOCUMENT_DATA_INVALID", Document.Create(type, null, Data(name: " "), Now).Errors[0].Code);
            Assert.Equal("DOCUMENT_DATA_INVALID", Document.Create(type, null, Data(size: 0), Now).Errors[0].Code);
            Assert.Equal("DOCUMENT_DATA_INVALID", Document.Create(type, null, Data(key: "../../etc/passwd"), Now).Errors[0].Code);
            Assert.Equal("DOCUMENT_DATA_INVALID", Document.Create(type, null, Data(key: new string('A', 32)), Now).Errors[0].Code);
            Assert.Equal("DOCUMENT_DATA_INVALID", Document.Create(type, null, Data(checksum: "short"), Now).Errors[0].Code);
        }

        [Fact]
        public void A_new_version_keeps_the_old_ones()
        {
            var document = NewDocument();

            var added = document.AddVersion(Data("cv-v2.pdf", 250), Now.AddDays(1));

            Assert.True(added.IsSuccess);
            Assert.Equal(2, document.CurrentVersionNumber);
            Assert.Equal("cv-v2.pdf", document.FileName);
            Assert.Equal(250, document.SizeBytes);
            Assert.Equal(Now.AddDays(1), document.UpdatedAt);
            Assert.Equal(2, document.Versions.Count);
            Assert.Equal("cv.pdf", document.Version(1)?.FileName);
            Assert.Null(document.Version(3));
        }

        [Fact]
        public void A_deleted_document_takes_no_new_version_and_cannot_be_deleted_again()
        {
            var document = NewDocument();
            var by = Guid.NewGuid();

            Assert.True(document.MarkDeleted(by, Now.AddDays(1)).IsSuccess);

            Assert.Equal(DocumentStatus.Deleted, document.Status);
            Assert.Equal(by, document.DeletedByUserId);
            Assert.Equal("DOCUMENT_DELETED", document.AddVersion(Data(), Now).Errors[0].Code);
            Assert.Equal("DOCUMENT_DELETED", document.MarkDeleted(by, Now).Errors[0].Code);
            Assert.Single(document.Versions);
        }

        // ---------- access ----------

        [Fact]
        public void An_access_is_valid_until_it_expires_or_is_revoked()
        {
            var access = DocumentAccess.Grant(Guid.NewGuid(), Guid.NewGuid(), DocumentAccessType.Read, Now.AddDays(1), Guid.NewGuid(), Now).Value;
            Assert.NotNull(access);

            Assert.True(access.IsValidAt(Now));
            Assert.True(access.IsValidAt(Now.AddHours(23)));
            Assert.False(access.IsValidAt(Now.AddDays(1)));

            var open = DocumentAccess.Grant(Guid.NewGuid(), Guid.NewGuid(), DocumentAccessType.Manage, null, null, Now).Value;
            Assert.NotNull(open);
            Assert.True(open.IsValidAt(Now.AddYears(5)));

            Assert.True(open.Revoke(Guid.NewGuid(), Now.AddDays(1)).IsSuccess);
            Assert.False(open.IsValidAt(Now));
            Assert.Equal("DOCUMENT_ACCESS_NOT_ACTIVE", open.Revoke(null, Now).Errors[0].Code);
        }

        [Fact]
        public void A_bad_access_is_refused()
        {
            Assert.Equal("DOCUMENT_ACCESS_INVALID", DocumentAccess.Grant(Guid.Empty, Guid.NewGuid(), DocumentAccessType.Read, null, null, Now).Errors[0].Code);
            Assert.Equal("DOCUMENT_ACCESS_INVALID", DocumentAccess.Grant(Guid.NewGuid(), Guid.Empty, DocumentAccessType.Read, null, null, Now).Errors[0].Code);
            Assert.Equal("DOCUMENT_ACCESS_INVALID", DocumentAccess.Grant(Guid.NewGuid(), Guid.NewGuid(), (DocumentAccessType)9, null, null, Now).Errors[0].Code);
            Assert.Equal("DOCUMENT_ACCESS_INVALID", DocumentAccess.Grant(Guid.NewGuid(), Guid.NewGuid(), DocumentAccessType.Read, Now, null, Now).Errors[0].Code);
        }
    }
}
