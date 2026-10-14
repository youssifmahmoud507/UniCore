using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Students;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class StudentAcademicProfileConfiguration : IEntityTypeConfiguration<StudentAcademicProfile>
    {
        public void Configure(EntityTypeBuilder<StudentAcademicProfile> builder)
        {
            builder.ToTable("StudentAcademicProfiles", "students");

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();

            builder.Property(p => p.AdmissionAcademicYearId).IsRequired();

            // Foreign keys to the academic tables (Developer 1) are added when those tables exist.
            builder.HasIndex(p => p.AdmissionAcademicYearId);
            builder.HasIndex(p => p.CurrentStudyPlanVersionId);
        }
    }

}
