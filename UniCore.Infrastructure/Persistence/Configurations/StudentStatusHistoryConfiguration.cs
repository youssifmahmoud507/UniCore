using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Students;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class StudentStatusHistoryConfiguration : IEntityTypeConfiguration<StudentStatusHistory>
    {
        public void Configure(EntityTypeBuilder<StudentStatusHistory> builder)
        {
            builder.ToTable("StudentStatusHistories", "students");

            builder.HasKey(h => h.Id);
            builder.Property(h => h.Id).ValueGeneratedNever();

            builder.Property(h => h.EffectiveDate).IsRequired();
            builder.Property(h => h.CreatedAt).IsRequired();
            builder.Property(h => h.Reason).HasMaxLength(500);

            builder.HasIndex(h => new { h.StudentId, h.EffectiveDate });
        }
    }

}
