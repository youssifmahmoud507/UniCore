using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public static class FinanceMapper
    {
        public static FeeDefinitionDto ToDto(FeeDefinition d) => new(d.Id, d.UniversityId, d.Code, d.Name, d.Description, d.DefaultAmount.Amount, d.IsActive, d.CreatedAt, d.UpdatedAt);

        public static FeeStructureDto ToDto(FeeStructure s) => new(s.Id, s.AcademicProgramId, s.AcademicSemesterId, s.Name, s.Status, s.Total.Amount, [.. s.Items.Select(i => new FeeStructureItemDto(i.Id, i.FeeDefinitionId, i.Amount.Amount))], s.CreatedAt, s.UpdatedAt, s.ActivatedAt, s.ArchivedAt);

        public static FeeStructureSummaryDto ToSummary(FeeStructure s) => new(s.Id, s.AcademicProgramId, s.AcademicSemesterId, s.Name, s.Status, s.Items.Count, s.Total.Amount);
    }

}
