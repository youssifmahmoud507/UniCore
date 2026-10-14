namespace UniCore.Application.Modules.Finance
{
    // IsReplay: the same Idempotency-Key and the same content came in again, so the original payment is returned.
    public sealed record PaymentOutcome(PaymentDto Payment, bool IsReplay);



}
