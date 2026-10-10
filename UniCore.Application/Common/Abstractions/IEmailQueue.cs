namespace UniCore.Application.Common.Abstractions
{
    public interface IEmailQueue
    {
        // Returns false when the queue is full. Delivery happens in the background.
        bool TryEnqueue(EmailMessage message);
    }

}
