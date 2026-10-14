using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Students;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class StudentProgramHistoryConfiguration : IEntityTypeConfiguration<StudentProgramHistory>
    {
        public void Configure(EntityTypeBuilder<StudentProgramHistory> builder)
        {
            builder.ToTable("StudentProgramHistories", "students");

            builder.HasKey(h => h.Id);
            builder.Property(h => h.Id).ValueGeneratedNever();

            builder.Property(h => h.StartDate).IsRequired();
            builder.Property(h => h.Reason).HasMaxLength(500);

            builder.Ignore(h => h.IsOpen);

            builder.HasIndex(h => new { h.StudentId, h.StartDate });
            builder.HasIndex(h => h.AcademicProgramId);
        }
    }

}
