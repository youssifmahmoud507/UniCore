using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity.Specifications
{
    public sealed class AllPermissionsSpecification : Specification<Permission>
    {
        public AllPermissionsSpecification()
        {
            ApplyOrderBy(p => p.Code);
        }
    }

}
