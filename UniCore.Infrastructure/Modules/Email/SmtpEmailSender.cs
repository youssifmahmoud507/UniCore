using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

using MailKit.Net.Smtp;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;

namespace UniCore.Infrastructure.Modules.Email
{
    public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
    {
        private readonly SmtpOptions _options = options.Value;
        private readonly ILogger<SmtpEmailSender> _logger = logger;

        public async Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            if (!MailboxAddress.TryParse(message.To, out var to))
            {
                return Result.Fail(Error.Validation("EMAIL_RECIPIENT_INVALID", "The recipient address is invalid."));
            }

            try
            {
                var mime = new MimeMessage();
                mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
                mime.To.Add(to);
                mime.Subject = message.Subject;
                mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

                using var client = new SmtpClient();
                client.Timeout = _options.TimeoutSeconds * 1000;

                await client.ConnectAsync(_options.Host, _options.Port, Map(_options.Security), cancellationToken);

                if (!string.IsNullOrEmpty(_options.Username))
                {
                    await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
                }

                await client.SendAsync(mime, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                return Result.Ok();
            }
            catch (OperationCanceledException)
            {
                return Result.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex)
            {
                // Never log the message, the recipient or the exception text: they can contain the OTP.
                _logger.LogError("Sending an email failed ({ExceptionType}).", ex.GetType().Name);
                return Result.Fail(CommonErrors.ExternalServiceUnavailable);
            }
        }

        private static SecureSocketOptions Map(SmtpSecurityMode mode) => mode switch
        {
            SmtpSecurityMode.StartTls => SecureSocketOptions.StartTls,
            SmtpSecurityMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => SecureSocketOptions.None
        };
    }



}
