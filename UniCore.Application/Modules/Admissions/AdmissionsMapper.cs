using UniCore.Domain.Modules.Admissions;

namespace UniCore.Application.Modules.Admissions
{
    // Entity -> DTO. The entities are never returned to the API directly.
    public static class AdmissionsMapper
    {
        public static ApplicantSummary ToSummary(Applicant a)
            => new(a.Id, a.FullName, a.Email, a.Phone, a.DateOfBirth, a.UserId, a.CreatedAt, a.UpdatedAt);

        public static RequirementSummary ToSummary(AdmissionRequirement r)
            => new(r.Id, r.AcademicProgramId, r.Code, r.Name, r.Description, r.IsMandatory, r.IsActive);

        public static ApplicationSummary ToSummary(AdmissionApplication a)
            => new(a.Id, a.ApplicantId, a.AcademicProgramId, a.AcademicYearId, a.Status, a.Deadline, a.CreatedAt, a.SubmittedAt, a.DecidedAt);

        public static ApplicationDetail ToDetail(AdmissionApplication a)
            => new(ToSummary(a),
                [.. a.Requirements.OrderBy(r => r.Code).Select(r => new ApplicationRequirementSummary(r.Id, r.AdmissionRequirementId, r.Code, r.Name, r.IsMandatory, r.DocumentId, r.ReviewStatus, r.ReviewNote, r.ReviewedAt))],
                [.. a.Decisions.OrderBy(d => d.DecidedAt).Select(d => new DecisionSummary(d.Id, d.Type, d.Reason, d.DecidedBy, d.DecidedAt))]);
    }
}
