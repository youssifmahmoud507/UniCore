namespace UniCore.Domain.Modules.Finance
{
    // F2 only issues invoices. PartiallyPaid, Paid and Cancelled get their behaviour in F3.
    public enum InvoiceStatus
    {
        Issued = 0,
        PartiallyPaid = 1,
        Paid = 2,
        Cancelled = 3
    }


}
