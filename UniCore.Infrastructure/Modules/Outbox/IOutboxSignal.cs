namespace UniCore.Infrastructure.Modules.Outbox
{
    // "There is new work in the outbox." Signalling can never fail the caller:
    // the scheduled run is the safety net.
    public interface IOutboxSignal
    {
        void Signal();

        // The job calls this when it starts, so the next Signal() can queue a new run.
        void Acknowledge();
    }



}
