using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;

namespace UniCore.IntegrationTests.Infrastructure
{
    // Replaces the SMTP sender in tests: every email stays in memory so a test can read the OTP.
    public sealed class CapturingEmailSender : IEmailSender
    {
        private readonly ConcurrentQueue<EmailMessage> _messages = new();

        public int Count => _messages.Count;

        public Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            _messages.Enqueue(message);
            return Task.FromResult(Result.Ok());
        }

        // Waits for an email that arrived after the first `skip` messages (read Count before the action).
        public async Task<EmailMessage?> WaitForAsync(Func<EmailMessage, bool> predicate, int skip, TimeSpan timeout)
        {
            var deadline = DateTimeOffset.UtcNow + timeout;

            while (DateTimeOffset.UtcNow < deadline)
            {
                var match = _messages.ToArray().Skip(skip).FirstOrDefault(predicate);

                if (match is not null)
                {
                    return match;
                }

                await Task.Delay(50);
            }

            return null;
        }
    }

}
