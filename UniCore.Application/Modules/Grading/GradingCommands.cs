namespace UniCore.Application.Modules.Grading
{
    public sealed record ScoreInput(Guid ComponentId, decimal Score);

    public sealed record EnterScoresCommand(Guid CourseSectionId, Guid StudentId, IReadOnlyList<ScoreInput>? Scores);
}
