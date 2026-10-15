using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.University
{
    // Entity -> DTO. The entities are never returned to the API directly.
    public static class UniversityMapper
    {
        public static UniversitySummary ToSummary(Domain.Modules.University.University u)
            => new(u.Id, u.Code, u.Name, u.Description, u.Email, u.Phone, u.Website, u.IsActive, u.CreatedAt, u.UpdatedAt);

        public static CampusSummary ToSummary(Campus c)
            => new(c.Id, c.UniversityId, c.Code, c.Name,
                new AddressSummary(c.Address.Street, c.Address.City, c.Address.State, c.Address.PostalCode, c.Address.Country),
                c.TimeZone, c.IsActive);

        public static FacultySummary ToSummary(Faculty f)
            => new(f.Id, f.UniversityId, f.Code, f.Name, f.Description, f.Email, f.Phone, f.IsActive, f.CreatedAt, f.UpdatedAt);

        public static DepartmentSummary ToSummary(Department d)
            => new(d.Id, d.FacultyId, d.Code, d.Name, d.Description, d.Email, d.Phone, d.IsActive);

        public static ProgramSummary ToSummary(AcademicProgram p)
            => new(p.Id, p.DepartmentId, p.Code, p.Name, p.DegreeType, p.DurationYears, p.TotalRequiredCredits, p.IsActive, p.CreatedAt, p.UpdatedAt);
    }
}
