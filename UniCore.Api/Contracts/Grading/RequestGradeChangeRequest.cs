namespace UniCore.Api.Contracts.Grading
{
    public sealed record RequestGradeChangeRequest(Guid? GradeId, Guid? AssessmentComponentId, decimal? NewScore, string? Reason);
}
