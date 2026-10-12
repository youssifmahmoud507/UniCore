using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.University
{
    // A building on a campus. The data model has no timestamps for buildings.
    public sealed class Building : AggregateRoot
    {
        private Building()
        {
        }

        public Guid CampusId { get; private set; }

        // Unique inside the campus (database index on CampusId + Code). Upper-case, never changed.
        public string Code { get; private set; } = string.Empty;

        public string Name { get; private set; } = string.Empty;
        public bool IsActive { get; private set; }

        // The application layer must check that the campus exists and is active before calling this.
        public static Result<Building> Create(Guid campusId, string? code, string? name)
        {
            if (campusId == Guid.Empty)
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

            return new Building
            {
                Id = Guid.NewGuid(),
                CampusId = campusId,
                Code = cleanCode,
                Name = cleanName,
                IsActive = true
            };
        }

        public Result Rename(string? name)
        {
            var cleanName = name?.Trim();

            if (cleanName is null || cleanName.Length < FieldRules.NameMinLength || cleanName.Length > FieldRules.NameMaxLength)
            {
                return Result.Fail(UniversityErrors.InvalidName);
            }

            Name = cleanName;
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

        // The caller looks up whether the building still has active rooms.
        public Result Deactivate(bool hasActiveRooms)
        {
            if (!IsActive)
            {
                return Result.Fail(UniversityErrors.AlreadyInactive);
            }

            if (hasActiveRooms)
            {
                return Result.Fail(UniversityErrors.BuildingHasActiveRooms);
            }

            IsActive = false;
            return Result.Ok();
        }
    }
}
