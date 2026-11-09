namespace UniCore.Api.Contracts.Grading
{
    public sealed record ScoreRequest(Guid? ComponentId, decimal? Score);

    // The scores of one student in one section (the section and student are in the route).
    public sealed record EnterScoresRequest(IReadOnlyList<ScoreRequest>? Scores);

    public sealed record ReturnGradesRequest(string? Reason);
}
