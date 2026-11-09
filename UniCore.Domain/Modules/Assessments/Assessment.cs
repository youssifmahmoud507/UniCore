using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Assessments
{
    // The grading scheme of one course section: its components and their weights. Aggregate root of its components so one
    // request changes the scheme as a whole (RowVersion). Dates against the semester are checked by the handler;
    // the rules inside one scheme (weights, names, limits, lifecycle) are decided here.
    // Lifecycle: Draft -> Published -> Closed; Draft or Published -> Cancelled. Only a Draft changes.
    public sealed class Assessment : AggregateRoot
    {
        public const int MaxComponents = 20;
        public const int NameMaxLength = 100;
        public const decimal MaxComponentScore = 1000m;

        private readonly List<AssessmentComponent> _components = [];

        private Assessment()
        {
        }

        public Guid CourseSectionId { get; private set; }
        public AssessmentStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }
        public DateTimeOffset? PublishedAt { get; private set; }
        public IReadOnlyList<AssessmentComponent> Components => _components.AsReadOnly();

        public bool IsDraft => Status == AssessmentStatus.Draft;
        public decimal TotalWeight => _components.Sum(c => c.Weight);

        public static Result<Assessment> Create(Guid courseSectionId, DateTimeOffset now)
        {
            if (courseSectionId == Guid.Empty)
            {
                return AssessmentErrors.Invalid;
            }

            return new Assessment { Id = Guid.NewGuid(), CourseSectionId = courseSectionId, Status = AssessmentStatus.Draft, CreatedAt = now };
        }

        public Result<AssessmentComponent> AddComponent(string? name, ComponentType type, decimal weight, decimal maxScore, DateOnly? dueDate, DateTimeOffset now)
        {
            if (!IsDraft)
            {
                return AssessmentErrors.InvalidState;
            }

            var checkedInput = Validate(name, type, weight, maxScore, dueDate, except: null);
            if (checkedInput.IsFailure)
            {
                return Result<AssessmentComponent>.Fail(checkedInput.Errors);
            }

            if (_components.Count >= MaxComponents)
            {
                return AssessmentErrors.TooManyComponents;
            }

            var component = AssessmentComponent.Create(Id, checkedInput.Value!, type, weight, maxScore, dueDate);
            _components.Add(component);
            UpdatedAt = now;
            return component;
        }

        public Result UpdateComponent(Guid componentId, string? name, ComponentType type, decimal weight, decimal maxScore, DateOnly? dueDate, DateTimeOffset now)
        {
            if (!IsDraft)
            {
                return Result.Fail(AssessmentErrors.InvalidState);
            }

            var component = _components.FirstOrDefault(c => c.Id == componentId);
            if (component is null)
            {
                return Result.Fail(AssessmentErrors.ComponentNotFound);
            }

            var checkedInput = Validate(name, type, weight, maxScore, dueDate, except: component);
            if (checkedInput.IsFailure)
            {
                return Result.Fail(checkedInput.Errors);
            }

            component.Change(checkedInput.Value!, type, weight, maxScore, dueDate);
            UpdatedAt = now;
            return Result.Ok();
        }

        public Result RemoveComponent(Guid componentId, DateTimeOffset now)
        {
            if (!IsDraft)
            {
                return Result.Fail(AssessmentErrors.InvalidState);
            }

            var component = _components.FirstOrDefault(c => c.Id == componentId);
            if (component is null)
            {
                return Result.Fail(AssessmentErrors.ComponentNotFound);
            }

            _components.Remove(component);
            UpdatedAt = now;
            return Result.Ok();
        }

        // Draft -> Published. The weights must total exactly 100 and every component needs a due date.
        public Result Publish(DateTimeOffset now)
        {
            if (!IsDraft)
            {
                return Result.Fail(AssessmentErrors.InvalidState);
            }

            if (_components.Count == 0)
            {
                return Result.Fail(AssessmentErrors.NoComponents);
            }

            if (TotalWeight != 100m)
            {
                return Result.Fail(AssessmentErrors.WeightNotHundred);
            }

            if (_components.Any(c => c.DueDate is null))
            {
                return Result.Fail(AssessmentErrors.DueDateMissing);
            }

            Status = AssessmentStatus.Published;
            PublishedAt = now;
            UpdatedAt = now;

            RaiseDomainEvent(new AssessmentPublished(Guid.NewGuid(), now, Id, CourseSectionId, _components.Count));
            return Result.Ok();
        }

        public Result Close(DateTimeOffset now)
        {
            if (Status != AssessmentStatus.Published)
            {
                return Result.Fail(AssessmentErrors.InvalidState);
            }

            Status = AssessmentStatus.Closed;
            UpdatedAt = now;

            RaiseDomainEvent(new AssessmentClosed(Guid.NewGuid(), now, Id, CourseSectionId));
            return Result.Ok();
        }

        // A Closed scheme stays: grades were entered against it.
        public Result Cancel(DateTimeOffset now)
        {
            if (Status is not (AssessmentStatus.Draft or AssessmentStatus.Published))
            {
                return Result.Fail(AssessmentErrors.InvalidState);
            }

            var wasPublished = Status == AssessmentStatus.Published;

            Status = AssessmentStatus.Cancelled;
            UpdatedAt = now;

            if (wasPublished)
            {
                RaiseDomainEvent(new AssessmentCancelled(Guid.NewGuid(), now, Id, CourseSectionId));
            }

            return Result.Ok();
        }

        // Returns the trimmed name. `except` is the component being changed (it does not clash with itself).
        private Result<string> Validate(string? name, ComponentType type, decimal weight, decimal maxScore, DateOnly? dueDate, AssessmentComponent? except)
        {
            var clean = name?.Trim();

            if (string.IsNullOrWhiteSpace(clean) || clean.Length > NameMaxLength || !Enum.IsDefined(type)
                || weight <= 0 || weight > 100 || decimal.Round(weight, 2) != weight
                || maxScore <= 0 || maxScore > MaxComponentScore || decimal.Round(maxScore, 2) != maxScore
                || dueDate == default(DateOnly))
            {
                return Result<string>.Fail(AssessmentErrors.ComponentInvalid);
            }

            var others = _components.Where(c => c != except).ToList();

            if (others.Any(c => string.Equals(c.Name, clean, StringComparison.OrdinalIgnoreCase)))
            {
                return Result<string>.Fail(AssessmentErrors.ComponentNameExists);
            }

            if (type is ComponentType.Midterm or ComponentType.Final && others.Any(c => c.Type == type))
            {
                return Result<string>.Fail(AssessmentErrors.ComponentTypeExists);
            }

            if (others.Sum(c => c.Weight) + weight > 100m)
            {
                return Result<string>.Fail(AssessmentErrors.WeightExceeded);
            }

            return clean;
        }
    }
}
