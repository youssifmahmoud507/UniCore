using System.Net;
using UniCore.Application.Common.Abstractions;

namespace UniCore.Application.Modules.Identity
{
    public static class AuthEmails
    {
        public static EmailMessage PasswordResetOtp(string to, string appName, string otp, int expiresInMinutes)
        {
            var app = WebUtility.HtmlEncode(appName);

            var text =
                $"Your {appName} password reset code is: {otp}\n\n" +
                $"This code expires in {expiresInMinutes} minutes.\n" +
                "If you did not request it, ignore this email. Your password has not changed.";

            var html = $$"""
            <div style="font-family:Arial,Helvetica,sans-serif;max-width:480px;margin:0 auto;padding:24px;color:#222">
              <h2 style="margin:0 0 16px">{{app}}</h2>
              <p>Use this code to reset your password:</p>
              <p style="font-size:32px;letter-spacing:6px;font-weight:bold;margin:16px 0">{{otp}}</p>
              <p>This code expires in {{expiresInMinutes}} minutes.</p>
              <p style="color:#666;font-size:13px">If you did not request it, ignore this email. Your password has not changed.</p>
            </div>
            """;

            return new EmailMessage(to, $"{appName} password reset code", html, text);
        }

        public static EmailMessage PasswordChanged(string to, string appName)
        {
            var app = WebUtility.HtmlEncode(appName);

            var text =
                $"The password for your {appName} account was just changed.\n\n" +
                "If this was you, no action is needed.\n" +
                "If it was not you, contact support immediately.";

            var html = $$"""
            <div style="font-family:Arial,Helvetica,sans-serif;max-width:480px;margin:0 auto;padding:24px;color:#222">
              <h2 style="margin:0 0 16px">{{app}}</h2>
              <p>The password for your account was just changed.</p>
              <p>If this was you, no action is needed.</p>
              <p style="color:#b00020">If it was not you, contact support immediately.</p>
            </div>
            """;

            return new EmailMessage(to, $"Your {appName} password was changed", html, text);
        }
    }


}
