using UniCore.Domain.Modules.Students;

namespace UniCore.Api.Contracts.Students
{
    public sealed record UpdateAcademicProfileRequest(Guid? AcademicLevelId, AcademicStanding? AcademicStanding);
}
