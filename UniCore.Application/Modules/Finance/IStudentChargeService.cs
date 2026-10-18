using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Finance
{
    // The contract for Developer 1 (student services: certificates, transcripts...).
    // Idempotent: the same student and reference return the same charge (Created = false).
    // No authorization inside: the module that calls it checks who may ask for the service.
    public interface IStudentChargeService
    {
        Task<Result<ServiceChargeResult>> ChargeAsync(ServiceChargeRequest request, CancellationToken cancellationToken = default);
    }

}
