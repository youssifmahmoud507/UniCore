using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Finance
{
    // What was taken off an invoice, and why, as it was the day the invoice was issued.
    // Only Invoice creates these.
    public sealed class InvoiceReduction : BaseEntity
    {
        private InvoiceReduction()
        {
        }

        public Guid InvoiceId { get; private set; }
        public ReductionKind Kind { get; private set; }
        public Guid SourceId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public ReductionType Type { get; private set; }
        public decimal Value { get; private set; }
        public Money Amount { get; private set; }

        internal static InvoiceReduction Create(Guid invoiceId, ReductionKind kind, Guid sourceId, string name, ReductionType type, decimal value, Money amount) => new()
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            Kind = kind,
            SourceId = sourceId,
            Name = name,
            Type = type,
            Value = value,
            Amount = amount
        };
    }



}
