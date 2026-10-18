using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public static class ReductionsMapper
    {
        public static DiscountDto ToDto(Discount d) => new(d.Id, d.Code, d.Name, d.Description, d.Type, d.Value, d.ValidFrom, d.ValidTo, d.IsActive, d.CreatedAt, d.UpdatedAt);

        public static ScholarshipDto ToDto(Scholarship s) => new(s.Id, s.Code, s.Name, s.Description, s.Type, s.Value, s.IsActive, s.CreatedAt, s.UpdatedAt);

        public static StudentScholarshipDto ToDto(StudentScholarship a) => new(a.Id, a.StudentId, a.ScholarshipId, a.ValidFrom, a.ValidTo, a.Status, a.Reason, a.AwardedByUserId, a.CreatedAt, a.RevokedAt);

        public static InvoiceReductionDto ToDto(InvoiceReduction r) => new(r.Id, r.Kind, r.SourceId, r.Name, r.Type, r.Value, r.Amount.Amount);

        public static RefundDto ToDto(Refund r) => new(r.Id, r.PaymentId, r.InvoiceId, r.StudentId, r.Amount.Amount, r.Reason, r.Status, r.RequestedByUserId, r.RequestedAt, r.ReviewedByUserId, r.ReviewedAt, r.RejectionReason, r.ProcessedByUserId, r.ProcessedAt);
    }






}
