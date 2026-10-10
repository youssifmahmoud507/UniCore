namespace UniCore.Application.Common.Abstractions
{
    public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody);
}
