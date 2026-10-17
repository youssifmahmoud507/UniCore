using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Finance
{
    public class FeeDefinitionTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
        private static readonly Guid University = Guid.NewGuid();

        private static Money Amount(decimal value)
        {
            Assert.True(Money.Of(value).TryGetValue(out var money));
            return money;
        }

        private static FeeDefinition NewDefinition(string code = "TUITION")
        {
            var result = FeeDefinition.Create(University, code, "Tuition", "Per semester", Amount(5000m), Now);
            Assert.True(result.TryGetValue(out var definition));
            Assert.NotNull(definition);
            return definition;
        }

        [Fact]
        public void A_new_definition_is_active_and_keeps_its_fields()
        {
            var definition = NewDefinition();

            Assert.NotEqual(Guid.Empty, definition.Id);
            Assert.Equal(University, definition.UniversityId);
            Assert.Equal("TUITION", definition.Code);
            Assert.Equal("Tuition", definition.Name);
            Assert.Equal("Per semester", definition.Description);
            Assert.Equal(5000m, definition.DefaultAmount.Amount);
            Assert.True(definition.IsActive);
            Assert.Equal(Now, definition.CreatedAt);
            Assert.Null(definition.UpdatedAt);
        }

        [Theory]
        [InlineData("  tuition ", "TUITION")]
        [InlineData("lab_fee2", "LAB_FEE2")]
        public void The_code_is_trimmed_and_upper_cased(string input, string expected)
        {
            Assert.Equal(expected, FeeDefinition.NormalizeCode(input));
            Assert.Equal(expected, NewDefinition(input).Code);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("A")]
        [InlineData("HAS SPACE")]
        [InlineData("DASH-ED")]
        [InlineData("ACCÉNT")]
        public void A_bad_code_is_rejected(string? code)
        {
            Assert.Null(FeeDefinition.NormalizeCode(code));

            var result = FeeDefinition.Create(University, code, "Name", null, Amount(10m), Now);

            Assert.True(result.IsFailure);
            Assert.Equal("FEE_CODE_INVALID", result.Errors[0].Code);
        }

        [Fact]
        public void A_code_longer_than_the_limit_is_rejected()
        {
            Assert.Null(FeeDefinition.NormalizeCode(new string('A', FeeDefinition.MaxCodeLength + 1)));
            Assert.NotNull(FeeDefinition.NormalizeCode(new string('A', FeeDefinition.MaxCodeLength)));
        }

        [Fact]
        public void Bad_data_is_rejected()
        {
            Assert.Equal("FEE_DEFINITION_DATA_INVALID", FeeDefinition.Create(Guid.Empty, "X1", "Name", null, Amount(10m), Now).Errors[0].Code);
            Assert.Equal("FEE_DEFINITION_DATA_INVALID", FeeDefinition.Create(University, "X1", "  ", null, Amount(10m), Now).Errors[0].Code);
            Assert.Equal("FEE_DEFINITION_DATA_INVALID", FeeDefinition.Create(University, "X1", new string('N', FeeDefinition.MaxNameLength + 1), null, Amount(10m), Now).Errors[0].Code);
            Assert.Equal("FEE_DEFINITION_DATA_INVALID", FeeDefinition.Create(University, "X1", "Name", new string('D', FeeDefinition.MaxDescriptionLength + 1), Amount(10m), Now).Errors[0].Code);
        }

        [Fact]
        public void The_default_amount_must_be_positive()
        {
            Assert.Equal("FINANCE_AMOUNT_MUST_BE_POSITIVE", FeeDefinition.Create(University, "X1", "Name", null, Money.Zero, Now).Errors[0].Code);
            Assert.Equal("FINANCE_AMOUNT_MUST_BE_POSITIVE", FeeDefinition.Create(University, "X1", "Name", null, Amount(-5m), Now).Errors[0].Code);
        }

        [Fact]
        public void A_blank_description_becomes_null()
        {
            var definition = FeeDefinition.Create(University, "X1", "Name", "   ", Amount(10m), Now).Value;

            Assert.Null(definition?.Description);
        }

        [Fact]
        public void Update_changes_the_details_but_never_the_code()
        {
            var definition = NewDefinition();
            var later = Now.AddDays(1);

            var result = definition.Update(" New name ", null, Amount(6000.50m), later);

            Assert.True(result.IsSuccess);
            Assert.Equal("TUITION", definition.Code);
            Assert.Equal("New name", definition.Name);
            Assert.Null(definition.Description);
            Assert.Equal(6000.50m, definition.DefaultAmount.Amount);
            Assert.Equal(later, definition.UpdatedAt);
        }

        [Fact]
        public void A_failed_update_changes_nothing()
        {
            var definition = NewDefinition();

            var result = definition.Update("", "x", Amount(1m), Now.AddDays(1));

            Assert.True(result.IsFailure);
            Assert.Equal("Tuition", definition.Name);
            Assert.Null(definition.UpdatedAt);
        }

        [Fact]
        public void Setting_the_state_it_already_has_does_nothing_and_a_change_sets_the_time()
        {
            var definition = NewDefinition();

            Assert.True(definition.SetActive(true, Now.AddDays(1)).IsSuccess);
            Assert.Null(definition.UpdatedAt);

            Assert.True(definition.SetActive(false, Now.AddDays(2)).IsSuccess);
            Assert.False(definition.IsActive);
            Assert.Equal(Now.AddDays(2), definition.UpdatedAt);
        }
    }




}
