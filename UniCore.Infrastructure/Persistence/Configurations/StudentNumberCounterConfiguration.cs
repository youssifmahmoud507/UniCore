using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Infrastructure.Modules.Students;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class StudentNumberCounterConfiguration : IEntityTypeConfiguration<StudentNumberCounter>
    {
        public void Configure(EntityTypeBuilder<StudentNumberCounter> builder)
        {
            builder.ToTable("StudentNumberCounters", "students");

            builder.HasKey(c => c.CounterKey);
            builder.Property(c => c.CounterKey).IsRequired().HasMaxLength(50);
            builder.Property(c => c.LastValue).IsRequired();
        }
    }

}
