using UniCore.Application.Common.Abstractions;

namespace UniCore.UnitTests.Modules.Identity
{
    internal sealed class FakeEmailQueue : IEmailQueue
    {
        public List<EmailMessage> Messages { get; } = new();
        public bool Full { get; set; }

        public Task<bool> TryEnqueueAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            if (Full)
            {
                return Task.FromResult(false);
            }

            Messages.Add(message);
            return Task.FromResult(true);
        }
    }



}
