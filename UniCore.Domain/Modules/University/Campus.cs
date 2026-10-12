using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Common.ValueObjects;

namespace UniCore.Domain.Modules.University
{
    // A physical campus of a university. The data model has no timestamps for campuses.
    public sealed class Campus : AggregateRoot
    {
        private Campus()
        {
        }

        public Guid UniversityId { get; private set; }

        // Unique inside the university (database index on UniversityId + Code). Upper-case, never changed.
        public string Code { get; private set; } = string.Empty;

        public string Name { get; private set; } = string.Empty;
        public Address Address { get; private set; } = null!; // always set by the factory; EF fills it when loading
        public string TimeZone { get; private set; } = string.Empty;
        public bool IsActive { get; private set; }

        // The application layer must check that the university exists and is active before calling this,
        // and that the time zone id exists on this machine (the domain only checks its format).
        public static Result<Campus> Create(Guid universityId, string? code, string? name, Address? address, string? timeZone)
        {
            if (universityId == Guid.Empty)
            {
                return UniversityErrors.InvalidParent;
            }

            var cleanCode = code?.Trim().ToUpperInvariant();

            if (cleanCode is null || !FieldRules.IsValidCode(cleanCode, FieldRules.CodeMinLength, FieldRules.CodeMaxLength))
            {
                return UniversityErrors.InvalidCode;
            }

            var cleanName = name?.Trim();

            if (cleanName is null || cleanName.Length < FieldRules.NameMinLength || cleanName.Length > FieldRules.NameMaxLength)
            {
                return UniversityErrors.InvalidName;
            }

            if (address is null)
            {
                return UniversityErrors.AddressRequired;
            }

            var cleanTimeZone = timeZone?.Trim();

            if (cleanTimeZone is null || !FieldRules.IsValidTimeZoneId(cleanTimeZone))
            {
                return UniversityErrors.InvalidTimeZone;
            }

            return new Campus
            {
                Id = Guid.NewGuid(),
                UniversityId = universityId,
                Code = cleanCode,
                Name = cleanName,
                Address = address,
                TimeZone = cleanTimeZone,
                IsActive = true
            };
        }

        public Result Update(string? name, Address? address, string? timeZone)
        {
            var cleanName = name?.Trim();

            if (cleanName is null || cleanName.Length < FieldRules.NameMinLength || cleanName.Length > FieldRules.NameMaxLength)
            {
                return Result.Fail(UniversityErrors.InvalidName);
            }

            if (address is null)
            {
                return Result.Fail(UniversityErrors.AddressRequired);
            }

            var cleanTimeZone = timeZone?.Trim();

            if (cleanTimeZone is null || !FieldRules.IsValidTimeZoneId(cleanTimeZone))
            {
                return Result.Fail(UniversityErrors.InvalidTimeZone);
            }

            Name = cleanName;
            Address = address;
            TimeZone = cleanTimeZone;
            return Result.Ok();
        }

        public Result Activate()
        {
            if (IsActive)
            {
                return Result.Fail(UniversityErrors.AlreadyActive);
            }

            IsActive = true;
            return Result.Ok();
        }

        // The caller looks up whether the campus still has active buildings.
        public Result Deactivate(bool hasActiveBuildings)
        {
            if (!IsActive)
            {
                return Result.Fail(UniversityErrors.AlreadyInactive);
            }

            if (hasActiveBuildings)
            {
                return Result.Fail(UniversityErrors.CampusHasActiveBuildings);
            }

            IsActive = false;
            return Result.Ok();
        }
    }
}
