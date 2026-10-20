using System.Net;
using UniCore.Application.Common.Abstractions;

namespace UniCore.Application.Modules.Notifications
{
    public static class NotificationEmails
    {
        public static EmailMessage Build(string appName, string to, string title, string content)
        {
            var app = WebUtility.HtmlEncode(appName);
            var heading = WebUtility.HtmlEncode(title);
            var body = WebUtility.HtmlEncode(content);

            var text = $"{title}\n\n{content}\n\n{appName}";

            var html = $$"""
            <div style="font-family:Arial,Helvetica,sans-serif;max-width:480px;margin:0 auto;padding:24px;color:#222">
              <h2 style="margin:0 0 16px">{{app}}</h2>
              <h3 style="margin:0 0 12px">{{heading}}</h3>
              <p>{{body}}</p>
              <p style="color:#666;font-size:13px">You get this message because of your notification settings in {{app}}.</p>
            </div>
            """;

            return new EmailMessage(to, $"{appName}: {title}", html, text);
        }
    }


}
