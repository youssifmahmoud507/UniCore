using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Application.Modules.Finance
{
    public sealed record FeeDefinitionDto(Guid Id, Guid UniversityId, string Code, string Name, string? Description, decimal DefaultAmount, UniCore.Domain.Modules.Finance.FeeBasis Basis, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

}
