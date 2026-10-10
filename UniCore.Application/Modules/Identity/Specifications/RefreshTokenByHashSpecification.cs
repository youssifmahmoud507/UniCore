using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity.Specifications
{
    public sealed class RefreshTokenByHashSpecification : Specification<RefreshToken>
    {
        public RefreshTokenByHashSpecification(string tokenHash)
        {
            SetCriteria(t => t.TokenHash == tokenHash);
            UseTracking(); // the handler modifies the token
        }
    }
}
