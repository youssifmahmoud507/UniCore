using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class DocumentsApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;
        private readonly RawHttp _raw = new(fixture.CreateHttpClient());

        private ApiClient Client => _api.Client;

        private static byte[] Pdf(int extra = 30, byte fill = (byte)'x') => [.. "%PDF-1.4\n"u8.ToArray(), .. Enumerable.Repeat(fill, extra)];

        private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

        private Task<RawResult> UploadAsync(string? token, string? type, string fileName, byte[] bytes, string? contentType = null)
            => _raw.PostFileAsync("/api/v1/documents", token, fileName, bytes, contentType, type is null ? null : new Dictionary<string, string> { ["typeCode"] = type });

        // The admin (a SuperAdmin) owns the documents. "A stranger" is a plain user: no role, no permissions.
        private async Task<(string Admin, string Stranger, Guid StrangerId)> PeopleAsync()
        {
            var stranger = await _api.CreateUserAsync("docstranger");
            return (await _api.AdminTokenAsync(), await _api.LoginTokenAsync(stranger.UserName, stranger.Password), stranger.Id);
        }

        // ---------- upload and download ----------

        [Fact]
        public async Task A_pdf_is_uploaded_described_and_downloaded_unchanged()
        {
            var admin = await _api.AdminTokenAsync();
            var bytes = Pdf(200);

            var uploaded = await UploadAsync(admin, "general", "My CV.pdf", bytes, "application/pdf");

            Assert.Equal(201, uploaded.Code);
            Assert.Equal("GENERAL", uploaded.Str("typeCode"));
            Assert.Equal("My CV.pdf", uploaded.Str("fileName"));
            Assert.Equal("application/pdf", uploaded.Str("contentType"));
            Assert.Equal(bytes.Length, uploaded.Prop("sizeBytes").GetInt64());
            Assert.Equal(1, uploaded.Prop("currentVersion").GetInt32());
            var id = uploaded.Str("id");

            var described = await Client.GetAsync($"/api/v1/documents/{id}", admin);
            Assert.Equal(200, described.Code);
            Assert.Equal(64, described.Prop("versions")[0].GetProperty("checksum").GetString()?.Length);

            var download = await _raw.GetAsync($"/api/v1/documents/{id}/download", admin);
            Assert.Equal(200, download.Code);
            Assert.Equal(bytes, download.Bytes);
            Assert.Equal("application/pdf", download.ContentType);
            Assert.Contains("attachment", download.Disposition);
            Assert.Contains("My CV.pdf", download.Disposition);
            Assert.Equal("nosniff", download.NoSniff);
        }

        [Fact]
        public async Task Files_are_stored_outside_the_web_under_random_names()
        {
            var admin = await _api.AdminTokenAsync();
            Assert.Equal(201, (await UploadAsync(admin, "GENERAL", "secret-name.pdf", Pdf())).Code);

            var files = Directory.GetFiles(ApiFixture.DocumentsRoot, "*", SearchOption.AllDirectories);

            Assert.NotEmpty(files);
            Assert.All(files, file => Assert.Matches("^[0-9a-f]{32}$", Path.GetFileName(file)));
            Assert.DoesNotContain(files, file => file.Contains("secret-name", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task A_file_is_checked_by_its_content_not_by_its_name()
        {
            var admin = await _api.AdminTokenAsync();

            var wrongContent = await UploadAsync(admin, "GENERAL", "fake.pdf", Png());
            Assert.Equal(400, wrongContent.Code);
            Assert.Equal("DOCUMENT_CONTENT_MISMATCH", wrongContent.ErrorCode);

            var wrongClaim = await UploadAsync(admin, "GENERAL", "claim.pdf", Pdf(), "image/png");
            Assert.Equal("DOCUMENT_CONTENT_MISMATCH", wrongClaim.ErrorCode);

            var program = await UploadAsync(admin, "GENERAL", "run.exe", Pdf());
            Assert.Equal("DOCUMENT_FORMAT_NOT_ALLOWED", program.ErrorCode);

            var empty = await UploadAsync(admin, "GENERAL", "empty.pdf", []);
            Assert.Equal("DOCUMENT_FILE_EMPTY", empty.ErrorCode);

            var noExtension = await UploadAsync(admin, "GENERAL", "noextension", Pdf());
            Assert.Equal("DOCUMENT_DATA_INVALID", noExtension.ErrorCode);
        }

        [Fact]
        public async Task A_file_bigger_than_the_limit_is_refused()
        {
            var admin = await _api.AdminTokenAsync();

            // The tests run with a 1 MB limit.
            var tooBig = await UploadAsync(admin, "GENERAL", "big.pdf", Pdf(1_100_000));

            Assert.Equal(400, tooBig.Code);
            Assert.Equal("DOCUMENT_FILE_TOO_LARGE", tooBig.ErrorCode);
        }

        [Fact]
        public async Task The_document_type_decides_which_formats_are_allowed()
        {
            var admin = await _api.AdminTokenAsync();
            var docx = new byte[] { 0x50, 0x4B, 0x03, 0x04, 1, 2, 3 };

            var notAllowed = await UploadAsync(admin, "ADMISSION_DOCUMENT", "paper.docx", docx);
            Assert.Equal("DOCUMENT_FORMAT_NOT_ALLOWED", notAllowed.ErrorCode);

            Assert.Equal(201, (await UploadAsync(admin, "GENERAL", "paper.docx", docx)).Code);

            var unknown = await UploadAsync(admin, "NOPE", "x.pdf", Pdf());
            Assert.Equal(404, unknown.Code);
            Assert.Equal("DOCUMENT_TYPE_NOT_FOUND", unknown.ErrorCode);
        }

        [Fact]
        public async Task Only_a_signed_in_person_with_the_upload_permission_can_upload()
        {
            var (_, stranger, _) = await PeopleAsync();

            Assert.Equal(401, (await UploadAsync(null, "GENERAL", "a.pdf", Pdf())).Code);
            Assert.Equal(403, (await UploadAsync(stranger, "GENERAL", "a.pdf", Pdf())).Code);
        }

        [Fact]
        public async Task A_request_without_a_file_is_a_validation_error()
        {
            var admin = await _api.AdminTokenAsync();

            var response = await Client.PostAsync("/api/v1/documents", new { typeCode = "GENERAL" }, admin);

            Assert.True(response.Code is 400 or 415);
        }

        // ---------- who can see what ----------

        [Fact]
        public async Task A_stranger_cannot_even_tell_that_a_document_exists_until_it_is_shared()
        {
            var (admin, stranger, strangerId) = await PeopleAsync();
            var id = (await UploadAsync(admin, "GENERAL", "private.pdf", Pdf())).Str("id");

            var hidden = await Client.GetAsync($"/api/v1/documents/{id}", stranger);
            Assert.Equal(404, hidden.Code);
            Assert.Equal("DOCUMENT_NOT_FOUND", hidden.ErrorCode);
            Assert.Equal(404, (await _raw.GetAsync($"/api/v1/documents/{id}/download", stranger)).Code);

            var granted = await Client.PostAsync($"/api/v1/documents/{id}/access", new { userId = strangerId, accessType = "Read" }, admin);
            Assert.Equal(201, granted.Code);

            Assert.Equal(200, (await Client.GetAsync($"/api/v1/documents/{id}", stranger)).Code);
            Assert.Equal(200, (await _raw.GetAsync($"/api/v1/documents/{id}/download", stranger)).Code);

            // A reader cannot add versions, share or delete.
            var version = await _raw.PostFileAsync($"/api/v1/documents/{id}/versions", stranger, "v2.pdf", Pdf(), null);
            Assert.Equal(404, version.Code);
            Assert.Equal(404, (await Client.DeleteAsync($"/api/v1/documents/{id}", stranger)).Code);

            var list = await Client.GetAsync($"/api/v1/documents/{id}/access", admin);
            var accessId = list.Body[0].GetProperty("id").GetString();

            Assert.Equal(200, (await Client.DeleteAsync($"/api/v1/documents/{id}/access/{accessId}", admin)).Code);
            Assert.Equal(404, (await Client.GetAsync($"/api/v1/documents/{id}", stranger)).Code);
        }

        [Fact]
        public async Task Sharing_is_checked()
        {
            var (admin, _, strangerId) = await PeopleAsync();
            var id = (await UploadAsync(admin, "GENERAL", "share.pdf", Pdf())).Str("id");
            var url = $"/api/v1/documents/{id}/access";

            Assert.Equal("DOCUMENT_ACCESS_INVALID", (await Client.PostAsync(url, new { userId = Guid.NewGuid() }, admin)).ErrorCode);
            Assert.Equal("DOCUMENT_ACCESS_INVALID", (await Client.PostAsync(url, new { userId = strangerId, expiresAt = DateTimeOffset.UtcNow.AddMinutes(-5) }, admin)).ErrorCode);

            Assert.Equal(201, (await Client.PostAsync(url, new { userId = strangerId }, admin)).Code);

            var again = await Client.PostAsync(url, new { userId = strangerId }, admin);
            Assert.Equal(409, again.Code);
            Assert.Equal("DOCUMENT_ACCESS_EXISTS", again.ErrorCode);

            var unknown = await Client.DeleteAsync($"{url}/{Guid.NewGuid()}", admin);
            Assert.Equal(404, unknown.Code);
            Assert.Equal("DOCUMENT_ACCESS_NOT_FOUND", unknown.ErrorCode);
        }

        [Fact]
        public async Task Every_read_of_a_sensitive_document_is_audited_and_other_reads_are_not()
        {
            var admin = await _api.AdminTokenAsync();
            var sensitive = (await UploadAsync(admin, "ADMISSION_DOCUMENT", "id-card.png", Png())).Str("id");
            var open = (await UploadAsync(admin, "GENERAL", "open.pdf", Pdf())).Str("id");

            Assert.Equal(200, (await Client.GetAsync($"/api/v1/documents/{sensitive}", admin)).Code);
            Assert.Equal(200, (await _raw.GetAsync($"/api/v1/documents/{sensitive}/download", admin)).Code);
            Assert.Equal(200, (await Client.GetAsync($"/api/v1/documents/{open}", admin)).Code);
            Assert.Equal(200, (await _raw.GetAsync($"/api/v1/documents/{open}/download", admin)).Code);

            var sensitiveAudit = await Client.GetAsync($"/api/v1/audit-logs?action=DOCUMENT_ACCESSED&entityId={sensitive}", admin);
            var openAudit = await Client.GetAsync($"/api/v1/audit-logs?action=DOCUMENT_ACCESSED&entityId={open}", admin);

            Assert.Equal(2, sensitiveAudit.Prop("totalCount").GetInt32());
            Assert.Equal(0, openAudit.Prop("totalCount").GetInt32());

            var uploaded = await Client.GetAsync($"/api/v1/audit-logs?action=DOCUMENT_UPLOADED&entityId={sensitive}", admin);
            Assert.Equal(1, uploaded.Prop("totalCount").GetInt32());
        }

        // ---------- versions, delete, list ----------

        [Fact]
        public async Task A_new_version_keeps_the_old_one_downloadable()
        {
            var admin = await _api.AdminTokenAsync();
            var first = Pdf(40, (byte)'a');
            var second = Pdf(90, (byte)'b');
            var id = (await UploadAsync(admin, "GENERAL", "report.pdf", first)).Str("id");

            var added = await _raw.PostFileAsync($"/api/v1/documents/{id}/versions", admin, "report-v2.pdf", second, null);

            Assert.Equal(201, added.Code);
            Assert.Equal(2, added.Prop("currentVersion").GetInt32());
            Assert.Equal("report-v2.pdf", added.Str("fileName"));
            Assert.Equal(2, added.Prop("versions").GetArrayLength());

            Assert.Equal(second, (await _raw.GetAsync($"/api/v1/documents/{id}/download", admin)).Bytes);
            Assert.Equal(first, (await _raw.GetAsync($"/api/v1/documents/{id}/download?version=1", admin)).Bytes);

            var missing = await _raw.GetAsync($"/api/v1/documents/{id}/download?version=9", admin);
            Assert.Equal(404, missing.Code);
            Assert.Equal("DOCUMENT_VERSION_NOT_FOUND", missing.ErrorCode);
        }

        [Fact]
        public async Task A_deleted_document_looks_like_it_never_existed_but_keeps_its_file()
        {
            var admin = await _api.AdminTokenAsync();
            var id = (await UploadAsync(admin, "GENERAL", "gone.pdf", Pdf())).Str("id");
            var filesBefore = Directory.GetFiles(ApiFixture.DocumentsRoot, "*", SearchOption.AllDirectories).Length;

            var deleted = await Client.DeleteAsync($"/api/v1/documents/{id}", admin);
            Assert.Equal(200, deleted.Code);
            Assert.Equal("Deleted", deleted.Str("status"));

            Assert.Equal(404, (await Client.GetAsync($"/api/v1/documents/{id}", admin)).Code);
            Assert.Equal(404, (await _raw.GetAsync($"/api/v1/documents/{id}/download", admin)).Code);
            Assert.Equal(404, (await Client.DeleteAsync($"/api/v1/documents/{id}", admin)).Code);
            Assert.Equal(filesBefore, Directory.GetFiles(ApiFixture.DocumentsRoot, "*", SearchOption.AllDirectories).Length);
        }

        [Fact]
        public async Task A_person_lists_only_their_own_active_documents()
        {
            var (admin, stranger, _) = await PeopleAsync();
            var keep = (await UploadAsync(admin, "GENERAL", "keep.pdf", Pdf())).Str("id");
            var drop = (await UploadAsync(admin, "GENERAL", "drop.pdf", Pdf())).Str("id");
            await Client.DeleteAsync($"/api/v1/documents/{drop}", admin);

            var mine = await Client.GetAsync("/api/v1/documents?pageSize=100", admin);
            var ids = Enumerable.Range(0, mine.Prop("items").GetArrayLength()).Select(i => mine.Prop("items")[i].GetProperty("id").GetString()).ToList();

            Assert.Contains(keep, ids);
            Assert.DoesNotContain(drop, ids);

            var theirs = await Client.GetAsync("/api/v1/documents", stranger);
            Assert.Equal(0, theirs.Prop("totalCount").GetInt32());
        }

        // ---------- document types ----------

        [Fact]
        public async Task The_default_types_exist_and_an_administrator_can_add_one()
        {
            var admin = await _api.AdminTokenAsync();
            var (_, stranger, _) = await PeopleAsync();

            var list = await Client.GetAsync("/api/v1/document-types", stranger);
            Assert.Equal(200, list.Code);
            var codes = Enumerable.Range(0, list.Body.GetArrayLength()).Select(i => list.Body[i].GetProperty("code").GetString()).ToList();
            Assert.Contains("ADMISSION_DOCUMENT", codes);
            Assert.Contains("ATTENDANCE_EXCUSE", codes);
            Assert.Contains("CERTIFICATE", codes);
            Assert.Contains("TRANSCRIPT", codes);
            Assert.Contains("GENERAL", codes);

            var code = $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant();
            var created = await Client.PostAsync("/api/v1/document-types", new { code, name = "Test type", isSensitive = false, allowedFormats = new[] { "Pdf", "Png" } }, admin);
            Assert.Equal(201, created.Code);
            Assert.Equal(2, created.Prop("allowedFormats").GetArrayLength());

            var duplicate = await Client.PostAsync("/api/v1/document-types", new { code, name = "Again", allowedFormats = new[] { "Pdf" } }, admin);
            Assert.Equal(409, duplicate.Code);
            Assert.Equal("DOCUMENT_TYPE_CODE_EXISTS", duplicate.ErrorCode);

            Assert.Equal(403, (await Client.PostAsync("/api/v1/document-types", new { code = "OTHER_TYPE", name = "x", allowedFormats = new[] { "Pdf" } }, stranger)).Code);
            Assert.Equal(401, (await Client.GetAsync("/api/v1/document-types")).Code);

            // The new type works.
            Assert.Equal(201, (await UploadAsync(admin, code, "t.png", Png())).Code);
            Assert.Equal("DOCUMENT_FORMAT_NOT_ALLOWED", (await UploadAsync(admin, code, "t.docx", [0x50, 0x4B, 0x03, 0x04, 1])).ErrorCode);
        }
    }


}
