namespace UniCore.Application.Modules.Idempotency
{
    public enum IdempotencyOutcome
    {
        // The key is new: run the request, then Complete or Abandon.
        Started,

        // The same request already succeeded: send the stored response.
        Replay,

        // The same request is still running.
        InProgress,

        // The key was used for a different request.
        Mismatch
    }

}
