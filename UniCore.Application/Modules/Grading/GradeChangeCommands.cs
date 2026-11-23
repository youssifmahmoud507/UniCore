namespace UniCore.Application.Modules.Grading
{
    public sealed record RequestGradeChangeCommand(Guid? GradeId, Guid? AssessmentComponentId, decimal? NewScore, string? Reason);
}
