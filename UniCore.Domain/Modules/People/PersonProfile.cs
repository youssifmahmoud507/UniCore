using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.People
{
    // The one record of a human being. A person can exist without a user account, and can be an Employee,
    // a Student (Developer 2's module) or both, always with this same profile.
    // NationalId is sensitive: unique when present, never logged, shown only to users with People.Manage.
    public sealed class PersonProfile : AggregateRoot
    {
        public const int MaxNameLength = 100;

        private PersonProfile()
        {
        }

        public string FirstName { get; private set; } = string.Empty;
        public string LastName { get; private set; } = string.Empty;
        public DateOnly? DateOfBirth { get; private set; }
        public Gender? Gender { get; private set; }
        public string? NationalId { get; private set; }
        public string? Email { get; private set; }
        public string? Phone { get; private set; }

        // The linked login account (Identity module), if any. Unique when present.
        public Guid? UserId { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public string FullName => $"{FirstName} {LastName}";

        // The application layer checks that the national id is not used by another person.
        public static Result<PersonProfile> Create(
            string? firstName, string? lastName, DateOnly? dateOfBirth, Gender? gender, string? nationalId,
            string? email, string? phone, DateOnly today, DateTimeOffset now)
        {
            var person = new PersonProfile { Id = Guid.NewGuid(), CreatedAt = now };

            var details = person.ApplyDetails(firstName, lastName, dateOfBirth, gender, nationalId, email, phone, today);
            if (details.IsFailure)
            {
                return Result<PersonProfile>.Fail(details.Errors);
            }

            return person;
        }

        public Result Update(
            string? firstName, string? lastName, DateOnly? dateOfBirth, Gender? gender, string? nationalId,
            string? email, string? phone, DateOnly today, DateTimeOffset now)
        {
            var details = ApplyDetails(firstName, lastName, dateOfBirth, gender, nationalId, email, phone, today);
            if (details.IsFailure)
            {
                return details;
            }

            UpdatedAt = now;
            return Result.Ok();
        }

        // The caller checks that no other person is linked to this user.
        public Result LinkUser(Guid userId, DateTimeOffset now)
        {
            if (userId == Guid.Empty)
            {
                return Result.Fail(PeopleErrors.InvalidUser);
            }

            if (UserId is not null)
            {
                return Result.Fail(PeopleErrors.PersonAlreadyLinked);
            }

            UserId = userId;
            UpdatedAt = now;
            return Result.Ok();
        }

        private Result ApplyDetails(
            string? firstName, string? lastName, DateOnly? dateOfBirth, Gender? gender, string? nationalId,
            string? email, string? phone, DateOnly today)
        {
            var first = firstName?.Trim();
            var last = lastName?.Trim();
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(last) || first.Length > MaxNameLength || last.Length > MaxNameLength)
            {
                return Result.Fail(PeopleErrors.InvalidName);
            }

            if (dateOfBirth is { } dob && dob >= today)
            {
                return Result.Fail(PeopleErrors.InvalidDateOfBirth);
            }

            if (gender is { } g && !Enum.IsDefined(g))
            {
                return Result.Fail(PeopleErrors.InvalidGender);
            }

            var cleanNationalId = FieldRules.NormalizeOptional(nationalId)?.ToUpperInvariant();
            if (cleanNationalId is not null && !IsValidNationalId(cleanNationalId))
            {
                return Result.Fail(PeopleErrors.InvalidNationalId);
            }

            var cleanEmail = FieldRules.NormalizeOptional(email);
            if (cleanEmail is not null && (cleanEmail.Length > FieldRules.EmailMaxLength || !FieldRules.IsValidEmail(cleanEmail)))
            {
                return Result.Fail(PeopleErrors.InvalidEmail);
            }

            var cleanPhone = FieldRules.NormalizeOptional(phone);
            if (cleanPhone is not null && !FieldRules.IsValidPhone(cleanPhone))
            {
                return Result.Fail(PeopleErrors.InvalidPhone);
            }

            FirstName = first;
            LastName = last;
            DateOfBirth = dateOfBirth;
            Gender = gender;
            NationalId = cleanNationalId;
            Email = cleanEmail;
            Phone = cleanPhone;
            return Result.Ok();
        }

        private static bool IsValidNationalId(string value)
            => value.Length is >= 5 and <= 30 && value.All(char.IsAsciiLetterOrDigit);
    }
}
