using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public static class FinancialHoldMapper
    {
        private static readonly FinancialHoldArea[] SingleAreas =
        [
            FinancialHoldArea.Registration,
            FinancialHoldArea.Graduation,
            FinancialHoldArea.Services
        ];

        public static FinancialHoldDto ToDto(FinancialHold h) => new(h.Id, h.StudentId, h.Reason, [.. SingleAreas.Where(area => h.Areas.HasFlag(area))], h.Status, h.OverdueAmount.Amount, h.Note, h.PlacedByUserId, h.PlacedAt, h.ReleasedAt, h.ReleasedByUserId, h.ReleaseNote);
    }




}
