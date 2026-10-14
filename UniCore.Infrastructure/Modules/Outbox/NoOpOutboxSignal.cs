namespace UniCore.Infrastructure.Modules.Outbox
{
    public sealed class NoOpOutboxSignal : IOutboxSignal
    {
        public void Signal()
        {
        }

        public void Acknowledge()
        {
        }
    }



}
