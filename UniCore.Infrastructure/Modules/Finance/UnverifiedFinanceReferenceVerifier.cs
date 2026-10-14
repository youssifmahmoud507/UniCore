using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Modules.Finance;
using UniCore.Domain.Common.Results;

namespace UniCore.Infrastructure.Modules.Finance
{
    // TEMPORARY. Accepts every university, program and semester id until Developer 1's modules exist.
    // Delete it when his verifier is registered.
    public sealed class UnverifiedFinanceReferenceVerifier : IFinanceReferenceVerifier
    {
        public Task<Result> VerifyUniversityAsync(Guid universityId, CancellationToken cancellationToken = default) => Task.FromResult(Result.Ok());

        public Task<Result> VerifyProgramAsync(Guid academicProgramId, CancellationToken cancellationToken = default) => Task.FromResult(Result.Ok());

        public Task<Result> VerifySemesterAsync(Guid academicSemesterId, CancellationToken cancellationToken = default) => Task.FromResult(Result.Ok());
    }

}
