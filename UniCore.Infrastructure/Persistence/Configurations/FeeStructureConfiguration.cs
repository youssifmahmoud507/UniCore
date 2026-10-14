using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class FeeStructureConfiguration : IEntityTypeConfiguration<FeeStructure>
    {
        public void Configure(EntityTypeBuilder<FeeStructure> builder)
        {
            builder.ToTable("FeeStructures", "finance");

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();

            builder.Property(s => s.Name).IsRequired().HasMaxLength(FeeStructure.MaxNameLength);
            builder.Property(s => s.Status).IsRequired();
            builder.Property(s => s.CreatedAt).IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(s => s.Total);
            builder.Ignore(s => s.DomainEvents);

            // The items belong to the structure: removing one from the list deletes its row.
            builder.HasMany(s => s.Items).WithOne().HasForeignKey(i => i.FeeStructureId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(s => s.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

            // AcademicProgramId and AcademicSemesterId have no foreign keys yet (Developer 1).
            // The guard against two Active structures for the same scope. NULLs count as equal in a unique
            // index, so "all programs, all semesters" is a scope of its own.
            builder.HasIndex(s => new { s.AcademicProgramId, s.AcademicSemesterId }).IsUnique().HasFilter("[Status] = 'Active'").HasDatabaseName("UX_FeeStructures_ActiveScope");

            builder.HasIndex(s => s.Status);
        }
    }

}
