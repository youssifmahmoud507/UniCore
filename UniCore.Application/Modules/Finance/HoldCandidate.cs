using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    // A student that needs a look: what they owe overdue today (zero when they only have a hold left to release).
    public sealed record HoldCandidate(Guid StudentId, Money Overdue);




}
