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
        public const string AuthPasswordResetRequested = "AUTH_PASSWORD_RESET_REQUESTED";
        public const string AuthPasswordResetThrottled = "AUTH_PASSWORD_RESET_THROTTLED";
        public const string AuthPasswordResetRequestFailed = "AUTH_PASSWORD_RESET_REQUEST_FAILED";
        public const string AuthOtpVerified = "AUTH_OTP_VERIFIED";
        public const string AuthOtpFailed = "AUTH_OTP_FAILED";
        public const string AuthOtpExpired = "AUTH_OTP_EXPIRED";
        public const string AuthOtpAttemptsExceeded = "AUTH_OTP_ATTEMPTS_EXCEEDED";
        public const string AuthPasswordResetCompleted = "AUTH_PASSWORD_RESET_COMPLETED";
        public const string EmailEnqueueFailed = "EMAIL_ENQUEUE_FAILED";
    }
}
