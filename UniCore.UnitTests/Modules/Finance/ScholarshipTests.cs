using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Finance
{
    public class ScholarshipTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Today = new(2026, 10, 17);

        private static Scholarship NewScholarship(ReductionType type = ReductionType.Percentage, decimal value = 50m)
        {
            var result = Scholarship.Create(" merit_a ", "Merit A", null, type, value, Now);
            Assert.True(result.TryGetValue(out var scholarship));
            Assert.NotNull(scholarship);
            return scholarship;
        }

        [Fact]
        public void A_new_scholarship_is_active_with_a_normalized_code()
        {
            var scholarship = NewScholarship();

            Assert.Equal("MERIT_A", scholarship.Code);
            Assert.Equal(50m, scholarship.Value);
            Assert.True(scholarship.IsActive);
        }

        [Fact]
        public void The_same_value_rules_as_a_discount_apply()
        {
            Assert.Equal("REDUCTION_CODE_INVALID", Scholarship.Create("x", "Name", null, ReductionType.Percentage, 10m, Now).Errors[0].Code);
            Assert.Equal("REDUCTION_DATA_INVALID", Scholarship.Create("S1", "", null, ReductionType.Percentage, 10m, Now).Errors[0].Code);
            Assert.Equal("FINANCE_PERCENT_INVALID", Scholarship.Create("S1", "Name", null, ReductionType.Percentage, 101m, Now).Errors[0].Code);
            Assert.Equal("FINANCE_AMOUNT_MUST_BE_POSITIVE", Scholarship.Create("S1", "Name", null, ReductionType.FixedAmount, -1m, Now).Errors[0].Code);
        }

        [Fact]
        public void Update_and_activation_work_like_a_discount()
        {
            var scholarship = NewScholarship();

            Assert.True(scholarship.Update("Merit B", "Better", ReductionType.FixedAmount, 1500m, Now.AddDays(1)).IsSuccess);
            Assert.Equal("MERIT_A", scholarship.Code);
            Assert.Equal(1500m, scholarship.Value);

            Assert.True(scholarship.SetActive(false, Now.AddDays(2)).IsSuccess);
            Assert.False(scholarship.IsActive);
        }

        // ---------- the award ----------

        [Fact]
        public void An_award_is_active_and_valid_inside_its_period()
        {
            var result = StudentScholarship.Award(Guid.NewGuid(), Guid.NewGuid(), Today, Today.AddMonths(6), "  Top of class  ", Guid.NewGuid(), Now);

            Assert.True(result.TryGetValue(out var award));
            Assert.NotNull(award);
            Assert.Equal(StudentScholarshipStatus.Active, award.Status);
            Assert.Equal("Top of class", award.Reason);
            Assert.True(award.IsValidOn(Today));
            Assert.True(award.IsValidOn(Today.AddMonths(6)));
            Assert.False(award.IsValidOn(Today.AddDays(-1)));
            Assert.False(award.IsValidOn(Today.AddMonths(6).AddDays(1)));
        }

        [Fact]
        public void An_award_without_an_end_date_never_expires()
        {
            var award = StudentScholarship.Award(Guid.NewGuid(), Guid.NewGuid(), Today, null, null, null, Now).Value;

            Assert.NotNull(award);
            Assert.True(award.IsValidOn(Today.AddYears(10)));
        }

        [Fact]
        public void A_bad_award_is_rejected()
        {
            Assert.Equal("SCHOLARSHIP_AWARD_INVALID", StudentScholarship.Award(Guid.Empty, Guid.NewGuid(), Today, null, null, null, Now).Errors[0].Code);
            Assert.Equal("SCHOLARSHIP_AWARD_INVALID", StudentScholarship.Award(Guid.NewGuid(), Guid.Empty, Today, null, null, null, Now).Errors[0].Code);
            Assert.Equal("SCHOLARSHIP_AWARD_INVALID", StudentScholarship.Award(Guid.NewGuid(), Guid.NewGuid(), Today, Today.AddDays(-1), null, null, Now).Errors[0].Code);
            Assert.Equal("SCHOLARSHIP_AWARD_INVALID", StudentScholarship.Award(Guid.NewGuid(), Guid.NewGuid(), Today, null, new string('r', StudentScholarship.MaxReasonLength + 1), null, Now).Errors[0].Code);
        }

        [Fact]
        public void A_revoked_award_is_no_longer_valid_and_cannot_be_revoked_again()
        {
            var award = StudentScholarship.Award(Guid.NewGuid(), Guid.NewGuid(), Today, null, null, null, Now).Value;
            Assert.NotNull(award);
            var by = Guid.NewGuid();

            Assert.True(award.Revoke(by, Now.AddDays(1)).IsSuccess);

            Assert.Equal(StudentScholarshipStatus.Revoked, award.Status);
            Assert.Equal(Now.AddDays(1), award.RevokedAt);
            Assert.Equal(by, award.RevokedByUserId);
            Assert.False(award.IsValidOn(Today));
            Assert.Equal("SCHOLARSHIP_AWARD_NOT_ACTIVE", award.Revoke(by, Now.AddDays(2)).Errors[0].Code);
        }
    }

}
