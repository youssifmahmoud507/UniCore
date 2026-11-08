using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Idempotency
{
    public interface IIdempotencyService
    {
        // Claims the key for this user, or tells what happened to the earlier request with the same key.
        Task<Result<IdempotencyBegin>> BeginAsync(string? key, Guid userId, string requestHash, CancellationToken cancellationToken = default);

        // The request succeeded: keep its response so a retry gets the same answer.
        Task<Result> CompleteAsync(Guid recordId, StoredResponse response, CancellationToken cancellationToken = default);

        // The request did not succeed (or cannot be stored): free the key so the client can try again.
        Task<Result> AbandonAsync(Guid recordId, CancellationToken cancellationToken = default);
    }

}
