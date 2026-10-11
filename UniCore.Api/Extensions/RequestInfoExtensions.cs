using Microsoft.AspNetCore.Mvc;
using UniCore.Application.Common.Abstractions;

namespace UniCore.Api.Extensions
{
    public static class RequestInfoExtensions
    {
        public static RequestInfo GetRequestInfo(this ControllerBase controller) => new(controller.HttpContext.Connection.RemoteIpAddress?.ToString(),controller.Request.Headers.UserAgent.ToString());
    }
}
