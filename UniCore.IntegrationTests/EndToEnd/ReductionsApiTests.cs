using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class ReductionsApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static string NewCode() => $"R{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        private async Task<Guid> NewDiscountAsync(string token, string type = "Percentage", decimal value = 10m, DateOnly? validTo = null)
        {
            var created = await Client.PostAsync("/api/v1/discounts", new { code = NewCode(), name = "Discount", description = "x", type, value, validTo }, token);
            Assert.Equal(201, created.Code);
            return created.GuidOf("id");
        }

        private async Task<Guid> NewScholarshipAsync(string token, string type = "Percentage", decimal value = 50m)
        {
            var created = await Client.PostAsync("/api/v1/scholarships", new { code = NewCode(), name = "Scholarship", description = "x", type, value }, token);
            Assert.Equal(201, created.Code);
            return created.GuidOf("id");
        }

        private Task<ApiResponse> AwardAsync(string token, Guid student, Guid scholarship, DateOnly? validTo = null)
            => Client.PostAsync("/api/v1/student-scholarships", new { studentId = student, scholarshipId = scholarship, validTo, reason = "Top of class" }, token);

        private Task<ApiResponse> IssueAsync(string token, Guid student, Guid semester, params Guid[] discounts)
            => Client.PostAsync("/api/v1/invoices", new { studentId = student, academicSemesterId = semester, discountIds = discounts }, token);

        // ---------- discount catalog ----------

        [Fact]
        public async Task Admin_manages_the_discount_catalog()
        {
            var admin = await _api.AdminTokenAsync();
            var code = NewCode().ToLowerInvariant();

            var created = await Client.PostAsync("/api/v1/discounts", new { code = $" {code} ", name = "Early bird", type = "FixedAmount", value = 150.50m }, admin);
            Assert.Equal(201, created.Code);
            Assert.Equal(code.ToUpperInvariant(), created.Str("code"));
            Assert.Equal("FixedAmount", created.Str("type"));
            Assert.Equal(150.50m, created.Prop("value").GetDecimal());
            Assert.True(created.Prop("isActive").GetBoolean());
            var id = created.GuidOf("id");

            var duplicate = await Client.PostAsync("/api/v1/discounts", new { code, name = "Again", type = "Percentage", value = 10m }, admin);
            Assert.Equal(409, duplicate.Code);
            Assert.Equal("DISCOUNT_CODE_ALREADY_EXISTS", duplicate.ErrorCode);

            var updated = await Client.PutAsync($"/api/v1/discounts/{id}", new { name = "Early bird 2", type = "Percentage", value = 12.5m }, admin);
            Assert.Equal(200, updated.Code);
            Assert.Equal("Percentage", updated.Str("type"));
            Assert.Equal(12.5m, updated.Prop("value").GetDecimal());

            var deactivated = await Client.PostAsync($"/api/v1/discounts/{id}/deactivate", null, admin);
            Assert.False(deactivated.Prop("isActive").GetBoolean());

            var found = await Client.GetAsync($"/api/v1/discounts?search={code}&isActive=false", admin);
            Assert.Equal(1, found.Prop("totalCount").GetInt32());

            Assert.Equal(200, (await Client.GetAsync($"/api/v1/discounts/{id}", admin)).Code);
            Assert.Equal(404, (await Client.GetAsync($"/api/v1/discounts/{Guid.NewGuid()}", admin)).Code);
        }

        [Fact]
        public async Task Bad_discount_data_is_a_validation_error_and_permissions_are_checked()
        {
            var admin = await _api.AdminTokenAsync();
            var user = await _api.CreateUserAsync("discnoperm");
            var token = await _api.LoginTokenAsync(user.UserName, user.Password);

            var tooBig = await Client.PostAsync("/api/v1/discounts", new { code = NewCode(), name = "x", type = "Percentage", value = 120m }, admin);
            var period = await Client.PostAsync("/api/v1/discounts", new { code = NewCode(), name = "x", type = "Percentage", value = 10m, validFrom = "2026-12-31", validTo = "2026-01-01" }, admin);
            var noType = await Client.PostAsync("/api/v1/discounts", new { code = NewCode(), name = "x", value = 10m }, admin);
            var badCode = await Client.PostAsync("/api/v1/discounts", new { code = "bad code", name = "x", type = "Percentage", value = 10m }, admin);

            Assert.Equal("FINANCE_PERCENT_INVALID", tooBig.ErrorCode);
            Assert.Equal("REDUCTION_PERIOD_INVALID", period.ErrorCode);
            Assert.Equal("REDUCTION_DATA_INVALID", noType.ErrorCode);
            Assert.Equal("REDUCTION_CODE_INVALID", badCode.ErrorCode);

            Assert.Equal(403, (await Client.PostAsync("/api/v1/discounts", new { code = NewCode(), name = "x", type = "Percentage", value = 10m }, token)).Code);
            Assert.Equal(403, (await Client.GetAsync("/api/v1/discounts", token)).Code);
            Assert.Equal(401, (await Client.GetAsync("/api/v1/discounts")).Code);
        }

        // ---------- scholarship catalog and awards ----------

        [Fact]
        public async Task Admin_manages_scholarships_and_awards_them()
        {
            var admin = await _api.AdminTokenAsync();
            var prepared = await InvoiceScenario.PrepareAsync(_api, admin, 1000m);
            var scholarship = await NewScholarshipAsync(admin);

            var duplicateCode = await Client.PostAsync("/api/v1/scholarships", new { code = NewCode(), name = "x", type = "Percentage", value = 10m }, admin);
            Assert.Equal(201, duplicateCode.Code);

            var award = await AwardAsync(admin, prepared.StudentId, scholarship);
            Assert.Equal(201, award.Code);
            Assert.Equal("Active", award.Str("status"));
            var awardId = award.GuidOf("id");

            var again = await AwardAsync(admin, prepared.StudentId, scholarship);
            Assert.Equal(409, again.Code);
            Assert.Equal("SCHOLARSHIP_AWARD_EXISTS", again.ErrorCode);

            var list = await Client.GetAsync($"/api/v1/student-scholarships?studentId={prepared.StudentId}&status=Active", admin);
            Assert.Equal(1, list.Prop("totalCount").GetInt32());

            var revoked = await Client.PostAsync($"/api/v1/student-scholarships/{awardId}/revoke", null, admin);
            Assert.Equal("Revoked", revoked.Str("status"));

            var revokedAgain = await Client.PostAsync($"/api/v1/student-scholarships/{awardId}/revoke", null, admin);
            Assert.Equal(422, revokedAgain.Code);
            Assert.Equal("SCHOLARSHIP_AWARD_NOT_ACTIVE", revokedAgain.ErrorCode);

            // After a revoke the scholarship can be awarded again.
            Assert.Equal(201, (await AwardAsync(admin, prepared.StudentId, scholarship)).Code);
        }

        [Fact]
        public async Task An_award_needs_a_real_student_and_an_active_scholarship()
        {
            var admin = await _api.AdminTokenAsync();
            var prepared = await InvoiceScenario.PrepareAsync(_api, admin, 1000m);
            var scholarship = await NewScholarshipAsync(admin);

            var noStudent = await AwardAsync(admin, Guid.NewGuid(), scholarship);
            Assert.Equal(404, noStudent.Code);
            Assert.Equal("STUDENT_NOT_FOUND", noStudent.ErrorCode);

            var noScholarship = await AwardAsync(admin, prepared.StudentId, Guid.NewGuid());
            Assert.Equal(404, noScholarship.Code);
            Assert.Equal("SCHOLARSHIP_NOT_FOUND", noScholarship.ErrorCode);

            await Client.PostAsync($"/api/v1/scholarships/{scholarship}/deactivate", null, admin);
            var inactive = await AwardAsync(admin, prepared.StudentId, scholarship);
            Assert.Equal(422, inactive.Code);
            Assert.Equal("SCHOLARSHIP_INACTIVE", inactive.ErrorCode);

            // A fresh, ACTIVE scholarship, so the period check is the one that fails.
            var active = await NewScholarshipAsync(admin);
            var badPeriod = await Client.PostAsync("/api/v1/student-scholarships", new { studentId = prepared.StudentId, scholarshipId = active, validFrom = "2026-12-31", validTo = "2026-01-01" }, admin);
            Assert.Equal("SCHOLARSHIP_AWARD_INVALID", badPeriod.ErrorCode);
        }

        // ---------- invoices with reductions ----------

        [Fact]
        public async Task An_invoice_gets_the_scholarship_by_itself_and_the_chosen_discount()
        {
            var admin = await _api.AdminTokenAsync();
            var prepared = await InvoiceScenario.PrepareAsync(_api, admin, 1000m);
            var scholarship = await NewScholarshipAsync(admin, "Percentage", 50m);
            var discount = await NewDiscountAsync(admin, "FixedAmount", 100m);
            Assert.Equal(201, (await AwardAsync(admin, prepared.StudentId, scholarship)).Code);

            var invoice = await IssueAsync(admin, prepared.StudentId, prepared.Semester, discount);

            Assert.Equal(201, invoice.Code);
            Assert.Equal(1000m, invoice.Prop("subtotal").GetDecimal());
            Assert.Equal(600m, invoice.Prop("discountAmount").GetDecimal());
            Assert.Equal(400m, invoice.Prop("totalAmount").GetDecimal());
            Assert.Equal(400m, invoice.Prop("balance").GetDecimal());
            Assert.Equal("Issued", invoice.Str("status"));

            var reductions = invoice.Prop("reductions");
            Assert.Equal(2, reductions.GetArrayLength());
            Assert.Equal("Scholarship", reductions[0].GetProperty("kind").GetString());
            Assert.Equal(500m, reductions[0].GetProperty("amount").GetDecimal());
            Assert.Equal("Discount", reductions[1].GetProperty("kind").GetString());
            Assert.Equal(100m, reductions[1].GetProperty("amount").GetDecimal());

            var fetched = await Client.GetAsync($"/api/v1/invoices/{invoice.GuidOf("id")}", admin);
            Assert.Equal(2, fetched.Prop("reductions").GetArrayLength());
        }

        [Fact]
        public async Task A_later_change_to_the_scholarship_does_not_touch_an_issued_invoice()
        {
            var admin = await _api.AdminTokenAsync();
            var prepared = await InvoiceScenario.PrepareAsync(_api, admin, 1000m);
            var scholarship = await NewScholarshipAsync(admin, "Percentage", 50m);
            await AwardAsync(admin, prepared.StudentId, scholarship);

            var invoice = await IssueAsync(admin, prepared.StudentId, prepared.Semester);
            await Client.PutAsync($"/api/v1/scholarships/{scholarship}", new { name = "Changed", type = "Percentage", value = 10m }, admin);

            var fetched = await Client.GetAsync($"/api/v1/invoices/{invoice.GuidOf("id")}", admin);
            Assert.Equal(500m, fetched.Prop("totalAmount").GetDecimal());
            Assert.Equal(50m, fetched.Prop("reductions")[0].GetProperty("value").GetDecimal());
        }

        [Fact]
        public async Task A_revoked_or_expired_scholarship_is_not_applied()
        {
            var admin = await _api.AdminTokenAsync();
            var revoked = await InvoiceScenario.PrepareAsync(_api, admin, 1000m);
            var expired = await InvoiceScenario.PrepareAsync(_api, admin, 1000m);
            var scholarship = await NewScholarshipAsync(admin, "Percentage", 50m);

            var award = await AwardAsync(admin, revoked.StudentId, scholarship);
            await Client.PostAsync($"/api/v1/student-scholarships/{award.GuidOf("id")}/revoke", null, admin);

            var past = await Client.PostAsync("/api/v1/student-scholarships", new { studentId = expired.StudentId, scholarshipId = scholarship, validFrom = AdmitSpec.Today.AddDays(-30), validTo = AdmitSpec.Today.AddDays(-1) }, admin);
            Assert.Equal(201, past.Code);

            var first = await IssueAsync(admin, revoked.StudentId, revoked.Semester);
            var second = await IssueAsync(admin, expired.StudentId, expired.Semester);

            Assert.Equal(1000m, first.Prop("totalAmount").GetDecimal());
            Assert.Equal(1000m, second.Prop("totalAmount").GetDecimal());
            Assert.Equal(0, first.Prop("reductions").GetArrayLength());
        }

        [Fact]
        public async Task A_discount_that_is_unknown_inactive_or_expired_is_refused()
        {
            var admin = await _api.AdminTokenAsync();
            var prepared = await InvoiceScenario.PrepareAsync(_api, admin, 1000m);
            var inactive = await NewDiscountAsync(admin);
            var expired = await NewDiscountAsync(admin, validTo: AdmitSpec.Today.AddDays(-1));
            await Client.PostAsync($"/api/v1/discounts/{inactive}/deactivate", null, admin);

            var unknown = await IssueAsync(admin, prepared.StudentId, prepared.Semester, Guid.NewGuid());
            Assert.Equal(404, unknown.Code);
            Assert.Equal("DISCOUNT_NOT_FOUND", unknown.ErrorCode);

            var notActive = await IssueAsync(admin, prepared.StudentId, prepared.Semester, inactive);
            Assert.Equal(422, notActive.Code);
            Assert.Equal("DISCOUNT_NOT_VALID", notActive.ErrorCode);

            Assert.Equal("DISCOUNT_NOT_VALID", (await IssueAsync(admin, prepared.StudentId, prepared.Semester, expired)).ErrorCode);

            var tooMany = new Guid[6];
            for (var i = 0; i < tooMany.Length; i++)
            {
                tooMany[i] = Guid.NewGuid();
            }

            var many = await IssueAsync(admin, prepared.StudentId, prepared.Semester, tooMany);
            Assert.Equal(400, many.Code);
            Assert.Equal("INVOICE_DATA_INVALID", many.ErrorCode);

            // Nothing was issued: the charges are still pending, so a clean issue works.
            Assert.Equal(201, (await IssueAsync(admin, prepared.StudentId, prepared.Semester)).Code);
        }

        [Fact]
        public async Task A_full_scholarship_gives_an_invoice_that_is_already_paid()
        {
            var admin = await _api.AdminTokenAsync();
            var prepared = await InvoiceScenario.PrepareAsync(_api, admin, 800m);
            var scholarship = await NewScholarshipAsync(admin, "Percentage", 100m);
            await AwardAsync(admin, prepared.StudentId, scholarship);

            var invoice = await IssueAsync(admin, prepared.StudentId, prepared.Semester);

            Assert.Equal(201, invoice.Code);
            Assert.Equal(0m, invoice.Prop("totalAmount").GetDecimal());
            Assert.Equal(0m, invoice.Prop("balance").GetDecimal());
            Assert.Equal("Paid", invoice.Str("status"));

            var pay = await Client.PostAsync(
                $"/api/v1/invoices/{invoice.GuidOf("id")}/payments",
                new { amount = 10m, method = "Cash" },
                admin,
                new Dictionary<string, string> { ["Idempotency-Key"] = $"key-{Guid.NewGuid():N}" });

            Assert.Equal(422, pay.Code);
            Assert.Equal("INVOICE_ALREADY_PAID", pay.ErrorCode);
        }

        [Fact]
        public async Task Reduction_changes_are_audited()
        {
            var admin = await _api.AdminTokenAsync();
            var discount = await NewDiscountAsync(admin);

            var audit = await Client.GetAsync($"/api/v1/audit-logs?action=DISCOUNT_CREATED&entityId={discount}", admin);

            Assert.Equal(200, audit.Code);
            Assert.Equal(1, audit.Prop("totalCount").GetInt32());
        }
    }


}
