using System.Threading.Channels;
using UniCore.Application.Common.Abstractions;

namespace UniCore.Infrastructure.Modules.Email
{
    // In-process queue. Replaced by the Outbox in the Outbox week; IEmailQueue does not change.
    public sealed class EmailQueue : IEmailQueue
    {
        private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
            new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true
            });

        public ChannelReader<EmailMessage> Reader => _channel.Reader;

        public bool TryEnqueue(EmailMessage message) => _channel.Writer.TryWrite(message);
    }



}
