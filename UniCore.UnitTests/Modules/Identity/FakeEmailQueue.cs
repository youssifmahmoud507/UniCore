using UniCore.Application.Common.Abstractions;

namespace UniCore.UnitTests.Modules.Identity
{
    internal sealed class FakeEmailQueue : IEmailQueue
    {
        public List<EmailMessage> Messages { get; } = new();
        public bool Full { get; set; }

        public bool TryEnqueue(EmailMessage message)
        {
            if (Full)
            {
                return false;
            }

            Messages.Add(message);
            return true;
        }
    }



}
