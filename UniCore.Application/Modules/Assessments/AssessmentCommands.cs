using UniCore.Domain.Modules.Assessments;

namespace UniCore.Application.Modules.Assessments
{
    public sealed record CreateAssessmentCommand(Guid CourseSectionId);

    // Used to add and to change a component.
    public sealed record ComponentCommand(string? Name, ComponentType Type, decimal Weight, decimal MaxScore, DateOnly? DueDate);
}
