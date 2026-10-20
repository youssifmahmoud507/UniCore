using UniCore.Domain.Modules.Admissions;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Admissions
{
    public class ApplicantAndRequirementTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Today = new(2026, 10, 12);
        private static readonly DateOnly Birth = new(2008, 5, 20);

        // ---------- Applicant ----------

        [Fact]
        public void Applicant_create_normalizes_and_stores_the_data()
        {
            var applicant = ResultAssert.Succeeded(Applicant.Create(" Mona Ali ", " Mona@Example.COM ", " +20 100 123 4567 ", Birth, Today, Now));

            Assert.NotEqual(Guid.Empty, applicant.Id);
            Assert.Equal("Mona Ali", applicant.FullName);
            Assert.Equal("mona@example.com", applicant.Email);
            Assert.Equal("+20 100 123 4567", applicant.Phone);
            Assert.Equal(Birth, applicant.DateOfBirth);
            Assert.Null(applicant.UserId);
            Assert.Equal(Now, applicant.CreatedAt);
        }

        [Fact]
        public void Applicant_blank_phone_becomes_null()
        {
            var applicant = ResultAssert.Succeeded(Applicant.Create("Mona Ali", "mona@example.com", "  ", Birth, Today, Now));

            Assert.Null(applicant.Phone);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(" ")]
        [InlineData("M")]
        public void Applicant_create_rejects_invalid_name(string? name)
        {
            ResultAssert.Failed(Applicant.Create(name, "mona@example.com", null, Birth, Today, Now), AdmissionsErrors.InvalidApplicantName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("mona")]
        [InlineData("mona@example")]
        public void Applicant_create_rejects_invalid_email(string? email)
        {
            ResultAssert.Failed(Applicant.Create("Mona Ali", email, null, Birth, Today, Now), AdmissionsErrors.InvalidApplicantEmail);
        }

        [Fact]
        public void Applicant_create_rejects_invalid_phone()
        {
            ResultAssert.Failed(Applicant.Create("Mona Ali", "mona@example.com", "abc", Birth, Today, Now), AdmissionsErrors.InvalidApplicantPhone);
        }

        [Fact]
        public void Applicant_create_rejects_a_birth_date_today_or_in_the_future()
        {
            ResultAssert.Failed(Applicant.Create("Mona Ali", "mona@example.com", null, Today, Today, Now), AdmissionsErrors.InvalidDateOfBirth);
            ResultAssert.Failed(Applicant.Create("Mona Ali", "mona@example.com", null, Today.AddDays(1), Today, Now), AdmissionsErrors.InvalidDateOfBirth);
        }

        [Fact]
        public void Applicant_create_rejects_a_birth_date_more_than_100_years_ago()
        {
            ResultAssert.Failed(Applicant.Create("Mona Ali", "mona@example.com", null, Today.AddYears(-101), Today, Now), AdmissionsErrors.InvalidDateOfBirth);
        }

        [Fact]
        public void Applicant_update_changes_the_data_and_sets_updated_at()
        {
            var applicant = ResultAssert.Succeeded(Applicant.Create("Mona Ali", "mona@example.com", null, Birth, Today, Now));
            var later = Now.AddDays(1);

            ResultAssert.Succeeded(applicant.UpdateDetails("Mona A. Ali", "mona2@example.com", "01001234567", Birth, Today, later));

            Assert.Equal("Mona A. Ali", applicant.FullName);
            Assert.Equal("mona2@example.com", applicant.Email);
            Assert.Equal(later, applicant.UpdatedAt);
        }

        [Fact]
        public void Applicant_failed_update_changes_nothing()
        {
            var applicant = ResultAssert.Succeeded(Applicant.Create("Mona Ali", "mona@example.com", null, Birth, Today, Now));

            ResultAssert.Failed(applicant.UpdateDetails("Mona A. Ali", "bad", null, Birth, Today, Now.AddDays(1)), AdmissionsErrors.InvalidApplicantEmail);

            Assert.Equal("Mona Ali", applicant.FullName);
            Assert.Null(applicant.UpdatedAt);
        }

        [Fact]
        public void Applicant_link_user_rejects_empty_id()
        {
            var applicant = ResultAssert.Succeeded(Applicant.Create("Mona Ali", "mona@example.com", null, Birth, Today, Now));

            ResultAssert.Failed(applicant.LinkUser(Guid.Empty, Now), AdmissionsErrors.InvalidReference);

            var userId = Guid.NewGuid();
            ResultAssert.Succeeded(applicant.LinkUser(userId, Now));
            Assert.Equal(userId, applicant.UserId);
        }

        // ---------- AdmissionRequirement ----------

        [Fact]
        public void Requirement_create_normalizes_code_and_starts_active()
        {
            var programId = Guid.NewGuid();

            var requirement = ResultAssert.Succeeded(AdmissionRequirement.Create(programId, " cert-1 ", " High school certificate ", " Original copy ", true, Now));

            Assert.Equal(programId, requirement.AcademicProgramId);
            Assert.Equal("CERT-1", requirement.Code);
            Assert.Equal("High school certificate", requirement.Name);
            Assert.Equal("Original copy", requirement.Description);
            Assert.True(requirement.IsMandatory);
            Assert.True(requirement.IsActive);
        }

        [Fact]
        public void Requirement_create_rejects_bad_input()
        {
            var programId = Guid.NewGuid();

            ResultAssert.Failed(AdmissionRequirement.Create(Guid.Empty, "CERT", "Certificate", null, true, Now), AdmissionsErrors.InvalidReference);
            ResultAssert.Failed(AdmissionRequirement.Create(programId, "C", "Certificate", null, true, Now), AdmissionsErrors.InvalidRequirementCode);
            ResultAssert.Failed(AdmissionRequirement.Create(programId, "CERT", " ", null, true, Now), AdmissionsErrors.InvalidRequirementName);
            ResultAssert.Failed(AdmissionRequirement.Create(programId, "CERT", "Certificate", new string('x', 1001), true, Now), AdmissionsErrors.InvalidRequirementDescription);
        }

        [Fact]
        public void Requirement_activate_and_deactivate_follow_the_state()
        {
            var requirement = ResultAssert.Succeeded(AdmissionRequirement.Create(Guid.NewGuid(), "CERT", "Certificate", null, true, Now));

            ResultAssert.Failed(requirement.Activate(Now), AdmissionsErrors.AlreadyActive);
            ResultAssert.Succeeded(requirement.Deactivate(Now));
            Assert.False(requirement.IsActive);
            ResultAssert.Failed(requirement.Deactivate(Now), AdmissionsErrors.AlreadyInactive);
            ResultAssert.Succeeded(requirement.Activate(Now));
            Assert.True(requirement.IsActive);
        }

        [Fact]
        public void Requirement_update_changes_details_but_not_the_code()
        {
            var requirement = ResultAssert.Succeeded(AdmissionRequirement.Create(Guid.NewGuid(), "CERT", "Certificate", null, true, Now));

            ResultAssert.Succeeded(requirement.UpdateDetails("Birth certificate", "Copy is enough", false, Now.AddDays(1)));

            Assert.Equal("CERT", requirement.Code);
            Assert.Equal("Birth certificate", requirement.Name);
            Assert.False(requirement.IsMandatory);
            Assert.Equal(Now.AddDays(1), requirement.UpdatedAt);
        }
    }
}
