using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class FeeCatalogApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static string NewCode() => $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        private async Task<ApiResponse> CreateDefinitionAsync(string token, string? code = null, Guid? university = null, decimal? amount = 5000m)
            => await Client.PostAsync("/api/v1/fee-definitions", new
            {
                universityId = university ?? Guid.NewGuid(),
                code = code ?? NewCode(),
                name = "Tuition",
                description = "Per semester",
                defaultAmount = amount
            }, token);

        private async Task<Guid> NewDefinitionIdAsync(string token)
        {
            var created = await CreateDefinitionAsync(token);
            Assert.Equal(201, created.Code);
            return created.GuidOf("id");
        }

        private async Task<Guid> NewStructureIdAsync(string token, Guid? program = null, Guid? semester = null)
        {
            var created = await Client.PostAsync("/api/v1/fee-structures", new
            {
                academicProgramId = program ?? Guid.NewGuid(),
                academicSemesterId = semester ?? Guid.NewGuid(),
                name = "Fall 2026"
            }, token);

            Assert.Equal(201, created.Code);
            return created.GuidOf("id");
        }

        // ---------- fee definitions ----------

        [Fact]
        public async Task Admin_creates_a_definition_and_the_code_is_normalized()
        {
            var admin = await _api.AdminTokenAsync();
            var code = NewCode().ToLowerInvariant();

            var created = await CreateDefinitionAsync(admin, $"  {code} ");

            Assert.Equal(201, created.Code);
            Assert.Equal(code.ToUpperInvariant(), created.Str("code"));
            Assert.Equal(5000m, created.Prop("defaultAmount").GetDecimal());
            Assert.True(created.Prop("isActive").GetBoolean());

            var fetched = await Client.GetAsync($"/api/v1/fee-definitions/{created.GuidOf("id")}", admin);
            Assert.Equal(200, fetched.Code);
        }

        [Fact]
        public async Task The_same_code_in_the_same_university_is_a_conflict_but_another_university_is_fine()
        {
            var admin = await _api.AdminTokenAsync();
            var university = Guid.NewGuid();
            var code = NewCode();

            Assert.Equal(201, (await CreateDefinitionAsync(admin, code, university)).Code);

            var duplicate = await CreateDefinitionAsync(admin, code, university);
            Assert.Equal(409, duplicate.Code);
            Assert.Equal("FEE_CODE_ALREADY_EXISTS", duplicate.ErrorCode);

            Assert.Equal(201, (await CreateDefinitionAsync(admin, code, Guid.NewGuid())).Code);
        }

        [Fact]
        public async Task Bad_amounts_and_codes_are_validation_errors()
        {
            var admin = await _api.AdminTokenAsync();

            var negative = await CreateDefinitionAsync(admin, amount: -10m);
            var zero = await CreateDefinitionAsync(admin, amount: 0m);
            var precision = await CreateDefinitionAsync(admin, amount: 10.123m);
            var missing = await CreateDefinitionAsync(admin, amount: null);
            var badCode = await CreateDefinitionAsync(admin, code: "bad code!");

            Assert.Equal(400, negative.Code);
            Assert.Equal("FINANCE_AMOUNT_MUST_BE_POSITIVE", negative.ErrorCode);
            Assert.Equal("FINANCE_AMOUNT_MUST_BE_POSITIVE", zero.ErrorCode);
            Assert.Equal("FINANCE_AMOUNT_PRECISION_INVALID", precision.ErrorCode);
            Assert.Equal("FINANCE_AMOUNT_REQUIRED", missing.ErrorCode);
            Assert.Equal(400, badCode.Code);
            Assert.Equal("FEE_CODE_INVALID", badCode.ErrorCode);
        }

        [Fact]
        public async Task A_user_without_permissions_cannot_create_or_list()
        {
            var user = await _api.CreateUserAsync("feenoperm");
            var token = await _api.LoginTokenAsync(user.UserName, user.Password);

            var create = await CreateDefinitionAsync(token);
            var list = await Client.GetAsync("/api/v1/fee-definitions", token);
            var anonymous = await Client.GetAsync("/api/v1/fee-definitions");

            Assert.Equal(403, create.Code);
            Assert.Equal(403, list.Code);
            Assert.Equal(401, anonymous.Code);
        }

        [Fact]
        public async Task A_definition_can_be_updated_deactivated_and_found_by_search()
        {
            var admin = await _api.AdminTokenAsync();
            var code = NewCode();
            var id = (await CreateDefinitionAsync(admin, code)).GuidOf("id");

            var updated = await Client.PutAsync($"/api/v1/fee-definitions/{id}", new { name = "Lab fee", description = (string?)null, defaultAmount = 250.75m }, admin);
            Assert.Equal(200, updated.Code);
            Assert.Equal("Lab fee", updated.Str("name"));
            Assert.Equal(250.75m, updated.Prop("defaultAmount").GetDecimal());
            Assert.Equal(code, updated.Str("code"));

            var deactivated = await Client.PostAsync($"/api/v1/fee-definitions/{id}/deactivate", null, admin);
            Assert.False(deactivated.Prop("isActive").GetBoolean());

            var found = await Client.GetAsync($"/api/v1/fee-definitions?search={code}&isActive=false", admin);
            Assert.Equal(200, found.Code);
            Assert.Equal(1, found.Prop("totalCount").GetInt32());

            var activated = await Client.PostAsync($"/api/v1/fee-definitions/{id}/activate", null, admin);
            Assert.True(activated.Prop("isActive").GetBoolean());
        }

        [Fact]
        public async Task An_unknown_definition_is_404()
        {
            var admin = await _api.AdminTokenAsync();

            var response = await Client.GetAsync($"/api/v1/fee-definitions/{Guid.NewGuid()}", admin);

            Assert.Equal(404, response.Code);
            Assert.Equal("FEE_DEFINITION_NOT_FOUND", response.ErrorCode);
        }

        // ---------- fee structures ----------

        [Fact]
        public async Task A_structure_is_built_item_by_item_and_activated()
        {
            var admin = await _api.AdminTokenAsync();
            var tuition = await NewDefinitionIdAsync(admin);
            var lab = await NewDefinitionIdAsync(admin);
            var structure = await NewStructureIdAsync(admin);
            var url = $"/api/v1/fee-structures/{structure}";

            // No amount: the definition's default (5000) is used.
            var first = await Client.PostAsync($"{url}/items", new { feeDefinitionId = tuition }, admin);
            Assert.Equal(200, first.Code);
            Assert.Equal(5000m, first.Prop("items")[0].GetProperty("amount").GetDecimal());

            var second = await Client.PostAsync($"{url}/items", new { feeDefinitionId = lab, amount = 250.50m }, admin);
            Assert.Equal(5250.50m, second.Prop("total").GetDecimal());

            var duplicate = await Client.PostAsync($"{url}/items", new { feeDefinitionId = lab, amount = 10m }, admin);
            Assert.Equal(409, duplicate.Code);
            Assert.Equal("FEE_ITEM_DUPLICATE", duplicate.ErrorCode);

            var labItem = second.Prop("items")[1].GetProperty("id").GetString();
            var changed = await Client.PutAsync($"{url}/items/{labItem}", new { amount = 300m }, admin);
            Assert.Equal(5300m, changed.Prop("total").GetDecimal());

            var removed = await Client.DeleteAsync($"{url}/items/{labItem}", admin);
            Assert.Equal(5000m, removed.Prop("total").GetDecimal());
            Assert.Equal(1, removed.Prop("items").GetArrayLength());

            var activated = await Client.PostAsync($"{url}/activate", null, admin);
            Assert.Equal(200, activated.Code);
            Assert.Equal("Active", activated.Str("status"));

            var frozen = await Client.PostAsync($"{url}/items", new { feeDefinitionId = lab }, admin);
            Assert.Equal(422, frozen.Code);
            Assert.Equal("FEE_STRUCTURE_NOT_DRAFT", frozen.ErrorCode);
        }

        [Fact]
        public async Task An_empty_structure_cannot_be_activated_and_an_inactive_fee_cannot_be_added()
        {
            var admin = await _api.AdminTokenAsync();
            var structure = await NewStructureIdAsync(admin);
            var definition = await NewDefinitionIdAsync(admin);

            var empty = await Client.PostAsync($"/api/v1/fee-structures/{structure}/activate", null, admin);
            Assert.Equal(422, empty.Code);
            Assert.Equal("FEE_STRUCTURE_EMPTY", empty.ErrorCode);

            await Client.PostAsync($"/api/v1/fee-definitions/{definition}/deactivate", null, admin);
            var inactive = await Client.PostAsync($"/api/v1/fee-structures/{structure}/items", new { feeDefinitionId = definition }, admin);
            Assert.Equal(422, inactive.Code);
            Assert.Equal("FEE_DEFINITION_INACTIVE", inactive.ErrorCode);

            var unknown = await Client.PostAsync($"/api/v1/fee-structures/{structure}/items", new { feeDefinitionId = Guid.NewGuid() }, admin);
            Assert.Equal(404, unknown.Code);
            Assert.Equal("FEE_DEFINITION_NOT_FOUND", unknown.ErrorCode);
        }

        [Fact]
        public async Task Only_one_structure_can_be_active_for_a_scope_until_the_first_is_archived()
        {
            var admin = await _api.AdminTokenAsync();
            var definition = await NewDefinitionIdAsync(admin);
            var program = Guid.NewGuid();
            var semester = Guid.NewGuid();

            var first = await NewStructureIdAsync(admin, program, semester);
            var second = await NewStructureIdAsync(admin, program, semester);

            foreach (var id in new[] { first, second })
            {
                Assert.Equal(200, (await Client.PostAsync($"/api/v1/fee-structures/{id}/items", new { feeDefinitionId = definition }, admin)).Code);
            }

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/fee-structures/{first}/activate", null, admin)).Code);

            var taken = await Client.PostAsync($"/api/v1/fee-structures/{second}/activate", null, admin);
            Assert.Equal(409, taken.Code);
            Assert.Equal("FEE_STRUCTURE_SCOPE_TAKEN", taken.ErrorCode);

            var archived = await Client.PostAsync($"/api/v1/fee-structures/{first}/archive", null, admin);
            Assert.Equal("Archived", archived.Str("status"));

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/fee-structures/{second}/activate", null, admin)).Code);
        }

        [Fact]
        public async Task Structures_can_be_listed_by_status_and_scope()
        {
            var admin = await _api.AdminTokenAsync();
            var program = Guid.NewGuid();
            var structure = await NewStructureIdAsync(admin, program, Guid.NewGuid());

            var list = await Client.GetAsync($"/api/v1/fee-structures?academicProgramId={program}&status=Draft", admin);

            Assert.Equal(200, list.Code);
            Assert.Equal(1, list.Prop("totalCount").GetInt32());
            Assert.Equal(structure.ToString(), list.Prop("items")[0].GetProperty("id").GetString());
            Assert.Equal(0, list.Prop("items")[0].GetProperty("itemCount").GetInt32());
        }

        [Fact]
        public async Task A_structure_for_all_programs_and_semesters_is_allowed()
        {
            var admin = await _api.AdminTokenAsync();

            var created = await Client.PostAsync("/api/v1/fee-structures", new { name = "Global fees" }, admin);

            Assert.Equal(201, created.Code);
            Assert.Equal(System.Text.Json.JsonValueKind.Null, created.Prop("academicProgramId").ValueKind);
        }

        [Fact]
        public async Task The_catalog_changes_are_audited()
        {
            var admin = await _api.AdminTokenAsync();
            var id = await NewDefinitionIdAsync(admin);

            var audit = await Client.GetAsync($"/api/v1/audit-logs?action=FEE_DEFINITION_CREATED&entityId={id}", admin);

            Assert.Equal(200, audit.Code);
            Assert.Equal(1, audit.Prop("totalCount").GetInt32());
        }
    }

}
