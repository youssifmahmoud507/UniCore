using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Offerings;

namespace UniCore.Infrastructure.Modules.Offerings.Configurations
{
    public sealed class CourseSectionConfiguration : IEntityTypeConfiguration<CourseSection>
    {
        public void Configure(EntityTypeBuilder<CourseSection> builder)
        {
            builder.ToTable("CourseSections", "offerings", table =>
            {
                table.HasCheckConstraint("CK_CourseSections_Capacity", "[Capacity] >= 1 AND [Capacity] <= 1000");
            });

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();

            builder.Ignore(s => s.DomainEvents);
            builder.Ignore(s => s.IsOpen);

            builder.Property(s => s.SectionCode).IsRequired().HasMaxLength(CourseSection.CodeMaxLength);
            builder.Property(s => s.Capacity).IsRequired();

            // Status is stored as text (max 50) by the convention in AppDbContext.
            builder.Property(s => s.Status).IsRequired();
            builder.Property(s => s.CreatedAt).IsRequired();

            // Registration changes sections concurrently (Week 9): the second of two overlapping saves fails with a concurrency conflict.
            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.HasOne<CourseOffering>().WithMany().HasForeignKey(s => s.CourseOfferingId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(s => new { s.CourseOfferingId, s.SectionCode }).IsUnique();
        }
    }
}
