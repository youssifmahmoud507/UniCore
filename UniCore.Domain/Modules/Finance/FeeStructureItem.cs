using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Finance
{
    // One priced line of a fee structure. Only FeeStructure creates and changes items.
    public sealed class FeeStructureItem : BaseEntity
    {
        private FeeStructureItem()
        {
        }

        public Guid FeeStructureId { get; private set; }
        public Guid FeeDefinitionId { get; private set; }
        public Money Amount { get; private set; }

        internal static FeeStructureItem Create(Guid feeStructureId, Guid feeDefinitionId, Money amount) => new()
        {
            Id = Guid.NewGuid(),
            FeeStructureId = feeStructureId,
            FeeDefinitionId = feeDefinitionId,
            Amount = amount
        };

        internal void SetAmount(Money amount) => Amount = amount;
    }


}
