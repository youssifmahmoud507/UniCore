namespace UniCore.Api.Contracts.Finance
{
    // StudentId = null evaluates every student.
    public sealed record EvaluateHoldsRequest(Guid? StudentId);
}
