namespace UniCore.Infrastructure.Modules.Finance
{
    // One row per year. Incremented atomically by InvoiceNumberGenerator.
    public sealed class InvoiceNumberCounter
    {
        public string CounterKey { get; set; } = string.Empty;

        public int LastValue { get; set; }
    }




}
