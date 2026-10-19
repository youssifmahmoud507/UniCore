using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Domain.Modules.Workflow
{
    // A linear list of steps, each approved by one role. Versioned: an instance is pinned to the version it
    // started with, so a definition that has instances is never edited; a new version is published instead.
    public sealed class WorkflowDefinition : AggregateRoot
    {
        public const int MinCodeLength = 3;
        public const int MaxCodeLength = 50;
        public const int MaxNameLength = 150;
        public const int MaxSteps = 10;

        private readonly List<WorkflowStep> _steps = [];

        private WorkflowDefinition()
        {
        }

        public string Code { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public int Version { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public IReadOnlyList<WorkflowStep> Steps => _steps.AsReadOnly();

        // Upper case letters, digits and underscore. Null means invalid.
        public static string? NormalizeCode(string? code)
        {
            var clean = code?.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(clean) || clean.Length < MinCodeLength || clean.Length > MaxCodeLength)
            {
                return null;
            }

            return clean.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_') ? clean : null;
        }

        public static Result<WorkflowDefinition> Create(string? code, string? name, int version, IReadOnlyList<WorkflowStepInput> steps, DateTimeOffset now)
        {
            var cleanCode = NormalizeCode(code);
            var cleanName = name?.Trim();

            if (cleanCode is null
                || string.IsNullOrEmpty(cleanName)
                || cleanName.Length < 2
                || cleanName.Length > MaxNameLength
                || version < 1
                || steps.Count < 1
                || steps.Count > MaxSteps)
            {
                return WorkflowErrors.DefinitionDataInvalid;
            }

            var definition = new WorkflowDefinition
            {
                Id = Guid.NewGuid(),
                Code = cleanCode,
                Name = cleanName,
                Version = version,
                IsActive = true,
                CreatedAt = now
            };

            var order = 1;

            foreach (var input in steps)
            {
                var stepName = input.Name?.Trim();
                var role = input.RequiredRole?.Trim();

                // Students and instructors never approve a request; SuperAdmin can always act, so it is never a step.
                if (string.IsNullOrEmpty(stepName)
                    || stepName.Length < 2
                    || stepName.Length > WorkflowStep.MaxNameLength
                    || string.IsNullOrEmpty(role)
                    || !RoleNames.All.Contains(role)
                    || role is RoleNames.Student or RoleNames.Instructor or RoleNames.SuperAdmin)
                {
                    return WorkflowErrors.DefinitionDataInvalid;
                }

                definition._steps.Add(WorkflowStep.Create(definition.Id, stepName, order, role));
                order++;
            }

            return definition;
        }

        // By Order, never by position: the database returns the steps in any order.
        public WorkflowStep FirstStep => _steps.OrderBy(s => s.Order).First();

        public WorkflowStep? StepById(Guid stepId) => _steps.FirstOrDefault(s => s.Id == stepId);

        public WorkflowStep? NextAfter(int order) => _steps.FirstOrDefault(s => s.Order == order + 1);

        public void Deactivate(DateTimeOffset now)
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            UpdatedAt = now;
        }

        public void Activate(DateTimeOffset now)
        {
            if (IsActive)
            {
                return;
            }

            IsActive = true;
            UpdatedAt = now;
        }
    }

}
