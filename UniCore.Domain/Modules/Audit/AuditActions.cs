using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Domain.Modules.Audit
{
    public static class AuditActions
    {
        public const string AuthLoginSucceeded = "AUTH_LOGIN_SUCCEEDED";
        public const string AuthLoginFailed = "AUTH_LOGIN_FAILED";
        public const string AuthAccountLocked = "AUTH_ACCOUNT_LOCKED";
        public const string AuthLogout = "AUTH_LOGOUT";
        public const string AuthTokenRefreshed = "AUTH_TOKEN_REFRESHED";
        public const string AuthTokenReuseDetected = "AUTH_TOKEN_REUSE_DETECTED";
    }
}
