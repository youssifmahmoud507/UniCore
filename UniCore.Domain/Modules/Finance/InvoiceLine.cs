using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    // One line of an invoice. Only Invoice creates lines.
    public sealed class InvoiceLine : BaseEntity
    {
        public const int MaxDescriptionLength = 150;
        public const int MaxQuantity = 1000;

        private InvoiceLine()
        {
        }

        public Guid InvoiceId { get; private set; }
        public string Description { get; private set; } = string.Empty;
        public int Quantity { get; private set; }
        public Money UnitPrice { get; private set; }
        public Money Amount { get; private set; }
        public Guid? StudentChargeId { get; private set; }

        internal static Result<InvoiceLine> Create(Guid invoiceId, string? description, int quantity, Money unitPrice, Guid? studentChargeId)
        {
            var text = description?.Trim();

            if (string.IsNullOrEmpty(text) || text.Length > MaxDescriptionLength || quantity < 1 || quantity > MaxQuantity || (studentChargeId is { } chargeId && chargeId == Guid.Empty))
            {
                return FinanceErrors.InvoiceDataInvalid;
            }

            if (!unitPrice.IsPositive)
            {
                return FinanceErrors.AmountMustBePositive;
            }

            var total = unitPrice.Multiply(quantity);
            if (!total.TryGetValue(out var amount))
            {
                return Result<InvoiceLine>.Fail(total.Errors);
            }

            return new InvoiceLine
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoiceId,
                Description = text,
                Quantity = quantity,
                UnitPrice = unitPrice,
                Amount = amount,
                StudentChargeId = studentChargeId
            };
        }
    }


}
