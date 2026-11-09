using UniCore.Domain.Modules.Assessments;

namespace UniCore.Application.Modules.Assessments
{
    public sealed record ComponentSummary(Guid Id, string Name, ComponentType Type, decimal Weight, decimal MaxScore, DateOnly? DueDate);

    public sealed record AssessmentSummary(
        Guid Id, Guid CourseSectionId, AssessmentStatus Status, decimal TotalWeight, IReadOnlyList<ComponentSummary> Components,
        DateTimeOffset CreatedAt, DateTimeOffset? PublishedAt);

    public static class AssessmentMapper
    {
        public static ComponentSummary ToSummary(AssessmentComponent c) => new(c.Id, c.Name, c.Type, c.Weight, c.MaxScore, c.DueDate);

        public static AssessmentSummary ToSummary(Assessment a)
            => new(a.Id, a.CourseSectionId, a.Status, a.TotalWeight, [.. a.Components.OrderBy(c => c.DueDate).ThenBy(c => c.Name).Select(ToSummary)], a.CreatedAt, a.PublishedAt);
    }
}
