using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Attendance;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.University;

namespace UniCore.Infrastructure.Modules.Attendance.Configurations
{
    public sealed class AttendancePolicyConfiguration : IEntityTypeConfiguration<AttendancePolicy>
    {
        public void Configure(EntityTypeBuilder<AttendancePolicy> builder)
        {
            builder.ToTable("AttendancePolicies", "attendance", table =>
            {
                table.HasCheckConstraint("CK_AttendancePolicies_OneTarget", "([AcademicProgramId] IS NULL AND [CourseId] IS NOT NULL) OR ([AcademicProgramId] IS NOT NULL AND [CourseId] IS NULL)");
                table.HasCheckConstraint("CK_AttendancePolicies_Percentages", "[MinimumPercentage] >= 0 AND [MinimumPercentage] <= [WarningPercentage] AND [WarningPercentage] <= 100");
            });

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();

            builder.Ignore(p => p.DomainEvents);

            builder.Property(p => p.MinimumPercentage).IsRequired().HasPrecision(5, 2);
            builder.Property(p => p.WarningPercentage).IsRequired().HasPrecision(5, 2);
            builder.Property(p => p.LateThresholdMinutes).IsRequired();
            builder.Property(p => p.IsExamBlocking).IsRequired();
            builder.Property(p => p.IsActive).IsRequired();
            builder.Property(p => p.CreatedAt).IsRequired();

            builder.HasOne<AcademicProgram>().WithMany().HasForeignKey(p => p.AcademicProgramId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Course>().WithMany().HasForeignKey(p => p.CourseId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            // One active policy per program and per course.
            builder.HasIndex(p => p.AcademicProgramId).IsUnique().HasDatabaseName("UX_AttendancePolicies_ActivePerProgram").HasFilter("[IsActive] = 1 AND [AcademicProgramId] IS NOT NULL");
            builder.HasIndex(p => p.CourseId).IsUnique().HasDatabaseName("UX_AttendancePolicies_ActivePerCourse").HasFilter("[IsActive] = 1 AND [CourseId] IS NOT NULL");
        }
    }

    public sealed class AttendanceSessionConfiguration : IEntityTypeConfiguration<AttendanceSession>
    {
        public void Configure(EntityTypeBuilder<AttendanceSession> builder)
        {
            builder.ToTable("AttendanceSessions", "attendance");

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();

            builder.Ignore(s => s.DomainEvents);
            builder.Ignore(s => s.IsOpen);

            builder.Property(s => s.SessionDate).IsRequired();
            builder.Property(s => s.StartTime).IsRequired();

            // Status is stored as text (max 50) by the convention in AppDbContext.
            builder.Property(s => s.Status).IsRequired();
            builder.Property(s => s.OpenedAt).IsRequired();

            // Two requests marking or closing the same session at once cannot both win.
            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.HasOne<CourseSection>().WithMany().HasForeignKey(s => s.CourseSectionId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(s => new { s.CourseSectionId, s.SessionDate, s.StartTime }).IsUnique();

            builder.HasMany(s => s.Records).WithOne().HasForeignKey(r => r.AttendanceSessionId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(s => s.Records).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
    {
        public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
        {
            builder.ToTable("AttendanceRecords", "attendance", table => table.HasCheckConstraint("CK_AttendanceRecords_MinutesLate", "[MinutesLate] IS NULL OR [MinutesLate] > 0"));

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.Status).IsRequired();
            builder.Property(r => r.MarkedAt).IsRequired();

            builder.HasOne<Student>().WithMany().HasForeignKey(r => r.StudentId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            // One record per student per session.
            builder.HasIndex(r => new { r.AttendanceSessionId, r.StudentId }).IsUnique();
            builder.HasIndex(r => r.StudentId);
        }
    }

    public sealed class AttendanceExcuseConfiguration : IEntityTypeConfiguration<AttendanceExcuse>
    {
        public void Configure(EntityTypeBuilder<AttendanceExcuse> builder)
        {
            builder.ToTable("AttendanceExcuses", "attendance");

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Ignore(e => e.DomainEvents);

            builder.Property(e => e.Reason).IsRequired().HasMaxLength(1000);
            builder.Property(e => e.ReviewNote).HasMaxLength(1000);
            builder.Property(e => e.Status).IsRequired();
            builder.Property(e => e.SubmittedAt).IsRequired();

            // Only the id of the document is kept; the file lives in the Documents module.
            builder.Property(e => e.DocumentId);

            builder.HasOne<AttendanceSession>().WithMany().HasForeignKey(e => e.AttendanceSessionId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<AttendanceRecord>().WithMany().HasForeignKey(e => e.AttendanceRecordId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Student>().WithMany().HasForeignKey(e => e.StudentId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            // One pending or accepted excuse per absence.
            builder.HasIndex(e => e.AttendanceRecordId).IsUnique().HasDatabaseName("UX_AttendanceExcuses_ActivePerRecord").HasFilter("[Status] <> 'Rejected'");
            builder.HasIndex(e => new { e.CourseSectionId, e.Status });
            builder.HasIndex(e => e.StudentId);
        }
    }

    public sealed class AttendanceWarningConfiguration : IEntityTypeConfiguration<AttendanceWarning>
    {
        public void Configure(EntityTypeBuilder<AttendanceWarning> builder)
        {
            builder.ToTable("AttendanceWarnings", "attendance");

            builder.HasKey(w => w.Id);
            builder.Property(w => w.Id).ValueGeneratedNever();

            builder.Ignore(w => w.DomainEvents);

            builder.Property(w => w.Percentage).IsRequired().HasPrecision(5, 2);
            builder.Property(w => w.Level).IsRequired();
            builder.Property(w => w.IsExamBlocking).IsRequired();
            builder.Property(w => w.IssuedAt).IsRequired();

            builder.HasOne<Student>().WithMany().HasForeignKey(w => w.StudentId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<CourseSection>().WithMany().HasForeignKey(w => w.CourseSectionId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(w => new { w.StudentId, w.CourseSectionId, w.IssuedAt });
        }
    }
}
