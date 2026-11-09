using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Examination;
using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.People;
using UniCore.Domain.Modules.University;

namespace UniCore.Infrastructure.Modules.Examination.Configurations
{
    public sealed class ExamSessionConfiguration : IEntityTypeConfiguration<ExamSession>
    {
        public void Configure(EntityTypeBuilder<ExamSession> builder)
        {
            builder.ToTable("ExamSessions", "examination", table => table.HasCheckConstraint("CK_ExamSessions_Dates", "[EndDate] >= [StartDate]"));

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();

            builder.Ignore(s => s.DomainEvents);
            builder.Ignore(s => s.IsOpen);

            builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
            builder.Property(s => s.ExamType).IsRequired();
            builder.Property(s => s.StartDate).IsRequired();
            builder.Property(s => s.EndDate).IsRequired();
            builder.Property(s => s.Status).IsRequired();
            builder.Property(s => s.CreatedAt).IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.HasOne<AcademicSemester>().WithMany().HasForeignKey(s => s.AcademicSemesterId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(s => new { s.AcademicSemesterId, s.Name }).IsUnique();
        }
    }

    public sealed class ExamConfiguration : IEntityTypeConfiguration<Exam>
    {
        public void Configure(EntityTypeBuilder<Exam> builder)
        {
            builder.ToTable("Exams", "examination", table =>
            {
                table.HasCheckConstraint("CK_Exams_Duration", "[DurationMinutes] >= 15 AND [DurationMinutes] <= 480");
                table.HasCheckConstraint("CK_Exams_MaxScore", "[MaxScore] > 0 AND [MaxScore] <= 1000");
            });

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Ignore(e => e.DomainEvents);
            builder.Ignore(e => e.IsDraft);
            builder.Ignore(e => e.TotalCapacity);

            builder.Property(e => e.ExamType).IsRequired();
            builder.Property(e => e.MaxScore).IsRequired().HasPrecision(7, 2);
            builder.Property(e => e.DurationMinutes).IsRequired();
            builder.Property(e => e.Status).IsRequired();
            builder.Property(e => e.CreatedAt).IsRequired();

            // Two requests changing the same exam at once cannot both win.
            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.HasOne<CourseSection>().WithMany().HasForeignKey(e => e.CourseSectionId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ExamSession>().WithMany().HasForeignKey(e => e.ExamSessionId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.Schedule).WithOne().HasForeignKey<ExamSchedule>(s => s.ExamId).IsRequired().OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(e => e.Rooms).WithOne().HasForeignKey(r => r.ExamId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(e => e.Rooms).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(e => e.Invigilators).WithOne().HasForeignKey(i => i.ExamId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(e => e.Invigilators).UsePropertyAccessMode(PropertyAccessMode.Field);

            // One live (not cancelled) exam per section, session and type.
            builder.HasIndex(e => new { e.ExamSessionId, e.CourseSectionId, e.ExamType }).IsUnique().HasDatabaseName("UX_Exams_ActivePerSectionAndType").HasFilter("[Status] <> 'Cancelled'");
            builder.HasIndex(e => e.CourseSectionId);
        }
    }

    public sealed class ExamScheduleConfiguration : IEntityTypeConfiguration<ExamSchedule>
    {
        public void Configure(EntityTypeBuilder<ExamSchedule> builder)
        {
            builder.ToTable("ExamSchedules", "examination", table => table.HasCheckConstraint("CK_ExamSchedules_Times", "[EndTime] > [StartTime]"));

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();

            builder.Property(s => s.ExamDate).IsRequired();
            builder.Property(s => s.StartTime).IsRequired();
            builder.Property(s => s.EndTime).IsRequired();

            builder.HasOne<ExamSession>().WithMany().HasForeignKey(s => s.ExamSessionId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(s => s.ExamId).IsUnique();
            builder.HasIndex(s => s.ExamDate);
        }
    }

    public sealed class ExamRoomConfiguration : IEntityTypeConfiguration<ExamRoom>
    {
        public void Configure(EntityTypeBuilder<ExamRoom> builder)
        {
            builder.ToTable("ExamRooms", "examination", table => table.HasCheckConstraint("CK_ExamRooms_Capacity", "[Capacity] > 0"));

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.Capacity).IsRequired();

            builder.HasOne<Room>().WithMany().HasForeignKey(r => r.RoomId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(r => new { r.ExamId, r.RoomId }).IsUnique();
            builder.HasIndex(r => r.RoomId);
        }
    }

    public sealed class ExamInvigilatorConfiguration : IEntityTypeConfiguration<ExamInvigilator>
    {
        public void Configure(EntityTypeBuilder<ExamInvigilator> builder)
        {
            builder.ToTable("ExamInvigilators", "examination");

            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id).ValueGeneratedNever();

            builder.Property(i => i.Role).IsRequired();

            builder.HasOne<Instructor>().WithMany().HasForeignKey(i => i.InstructorId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ExamRoom>().WithMany().HasForeignKey(i => i.ExamRoomId).IsRequired().OnDelete(DeleteBehavior.NoAction);

            // An instructor invigilates an exam once; one chief per room.
            builder.HasIndex(i => new { i.ExamId, i.InstructorId }).IsUnique();
            builder.HasIndex(i => new { i.ExamRoomId, i.Role }).IsUnique().HasDatabaseName("UX_ExamInvigilators_ChiefPerRoom").HasFilter("[Role] = 'Chief'");
            builder.HasIndex(i => i.InstructorId);
        }
    }
}
