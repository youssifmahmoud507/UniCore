using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.University;

namespace UniCore.Infrastructure.Modules.Calendar.Configurations
{
    public sealed class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
    {
        public void Configure(EntityTypeBuilder<AcademicYear> builder)
        {
            builder.ToTable("AcademicYears", "calendar", table =>
            {
                table.HasCheckConstraint("CK_AcademicYears_Dates", "[StartDate] < [EndDate]");
            });

            builder.HasKey(y => y.Id);
            builder.Property(y => y.Id).ValueGeneratedNever();
            builder.Ignore(y => y.DomainEvents);

            builder.Property(y => y.Name).IsRequired().HasMaxLength(FieldRules.NameMaxLength);

            builder.HasIndex(y => y.Name).IsUnique();

            // Exactly one current year: a unique index over only the rows where IsCurrent = 1.
            builder.HasIndex(y => y.IsCurrent).IsUnique().HasFilter("[IsCurrent] = 1");
        }
    }

    public sealed class AcademicTermConfiguration : IEntityTypeConfiguration<AcademicTerm>
    {
        public void Configure(EntityTypeBuilder<AcademicTerm> builder)
        {
            builder.ToTable("AcademicTerms", "calendar");

            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();
            builder.Ignore(t => t.DomainEvents);

            builder.Property(t => t.Code).IsRequired().HasMaxLength(FieldRules.CodeMaxLength);
            builder.Property(t => t.Name).IsRequired().HasMaxLength(FieldRules.NameMaxLength);

            builder.HasIndex(t => t.Code).IsUnique();
        }
    }

    public sealed class AcademicSemesterConfiguration : IEntityTypeConfiguration<AcademicSemester>
    {
        public void Configure(EntityTypeBuilder<AcademicSemester> builder)
        {
            builder.ToTable("AcademicSemesters", "calendar", table =>
            {
                table.HasCheckConstraint("CK_AcademicSemesters_Dates", "[StartDate] < [EndDate]");
            });

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Ignore(s => s.DomainEvents);

            // The window columns are plain nullable dates on this table. Windows is a computed view of them.
            builder.Ignore(s => s.Windows);
            builder.Ignore(s => s.IsClosed);

            // UQ(AcademicYearId, AcademicTermId): one semester per term per year.
            builder.HasIndex(s => new { s.AcademicYearId, s.AcademicTermId }).IsUnique();
            builder.HasIndex(s => s.Status);

            builder.HasOne<AcademicYear>().WithMany().HasForeignKey(s => s.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<AcademicTerm>().WithMany().HasForeignKey(s => s.AcademicTermId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class AcademicLevelConfiguration : IEntityTypeConfiguration<AcademicLevel>
    {
        public void Configure(EntityTypeBuilder<AcademicLevel> builder)
        {
            builder.ToTable("AcademicLevels", "calendar", table =>
            {
                table.HasCheckConstraint("CK_AcademicLevels_Order", "[Order] >= 1 AND [Order] <= 20");
                table.HasCheckConstraint("CK_AcademicLevels_RequiredCredits", "[RequiredCredits] >= 0 AND [RequiredCredits] <= 400");
            });

            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedNever();
            builder.Ignore(l => l.DomainEvents);

            builder.Property(l => l.Name).IsRequired().HasMaxLength(FieldRules.NameMaxLength);

            // UQ(AcademicProgramId, Order) and UQ(AcademicProgramId, Name)
            builder.HasIndex(l => new { l.AcademicProgramId, l.Order }).IsUnique();
            builder.HasIndex(l => new { l.AcademicProgramId, l.Name }).IsUnique();

            builder.HasOne<AcademicProgram>().WithMany().HasForeignKey(l => l.AcademicProgramId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
