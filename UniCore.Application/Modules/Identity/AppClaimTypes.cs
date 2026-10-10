using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Application.Modules.Identity
{
    // Raw JWT claim names. Inbound claim mapping is turned off, so these are used as they are.
    public static class AppClaimTypes
    {
        public const string Sub = "sub";
        public const string Jti = "jti";
        public const string Email = "email";
        public const string Name = "name";
        public const string Role = "role";
        public const string PersonId = "person_id";
    }
}
