using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.Admissions
{
    // A person applying to the university. This is NOT a student: a student is created only after an
    // application is accepted. Holds personal data, so it never appears in audit values or logs.
    public sealed class Applicant : AggregateRoot
    {
        public const int MaxAgeYears = 100;

        private Applicant()
        {
        }

        public string FullName { get; private set; } = string.Empty;

        // Lower-case, unique (database index). One applicant per email.
        public string Email { get; private set; } = string.Empty;

        public string? Phone { get; private set; }
        public DateOnly DateOfBirth { get; private set; }

        // The login account of the applicant, once the applicant portal exists. Optional.
        public Guid? UserId { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        // today comes from IClock (the caller), so the domain never reads the system time.
        public static Result<Applicant> Create(string? fullName, string? email, string? phone, DateOnly dateOfBirth, DateOnly today, DateTimeOffset now)
        {
            var applicant = new Applicant { Id = Guid.NewGuid(), CreatedAt = now };

            var applied = applicant.Apply(fullName, email, phone, dateOfBirth, today);

            if (applied.IsFailure)
            {
                return Result<Applicant>.Fail(applied.Errors);
            }

            return applicant;
        }

        public Result UpdateDetails(string? fullName, string? email, string? phone, DateOnly dateOfBirth, DateOnly today, DateTimeOffset now)
        {
            var applied = Apply(fullName, email, phone, dateOfBirth, today);

            if (applied.IsFailure)
            {
                return applied;
            }

            UpdatedAt = now;
            return Result.Ok();
        }

        public Result LinkUser(Guid userId, DateTimeOffset now)
        {
            if (userId == Guid.Empty)
            {
                return Result.Fail(AdmissionsErrors.InvalidReference);
            }

            UserId = userId;
            UpdatedAt = now;
            return Result.Ok();
        }

        private Result Apply(string? fullName, string? email, string? phone, DateOnly dateOfBirth, DateOnly today)
        {
            var cleanName = fullName?.Trim();
            if (cleanName is null || cleanName.Length < FieldRules.NameMinLength || cleanName.Length > FieldRules.NameMaxLength)
            {
                return Result.Fail(AdmissionsErrors.InvalidApplicantName);
            }

            var cleanEmail = email?.Trim().ToLowerInvariant();
            if (!FieldRules.IsValidEmail(cleanEmail))
            {
                return Result.Fail(AdmissionsErrors.InvalidApplicantEmail);
            }

            var cleanPhone = FieldRules.NormalizeOptional(phone);
            if (cleanPhone is not null && !FieldRules.IsValidPhone(cleanPhone))
            {
                return Result.Fail(AdmissionsErrors.InvalidApplicantPhone);
            }

            if (dateOfBirth >= today || dateOfBirth < today.AddYears(-MaxAgeYears))
            {
                return Result.Fail(AdmissionsErrors.InvalidDateOfBirth);
            }

            FullName = cleanName;
            Email = cleanEmail!;
            Phone = cleanPhone;
            DateOfBirth = dateOfBirth;
            return Result.Ok();
        }
    }
}
