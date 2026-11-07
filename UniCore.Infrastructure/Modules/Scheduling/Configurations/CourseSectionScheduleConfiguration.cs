using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.Scheduling;
using UniCore.Domain.Modules.University;

namespace UniCore.Infrastructure.Modules.Scheduling.Configurations
{
    public sealed class CourseSectionScheduleConfiguration : IEntityTypeConfiguration<CourseSectionSchedule>
    {
        public void Configure(EntityTypeBuilder<CourseSectionSchedule> builder)
        {
            builder.ToTable("CourseSectionSchedules", "scheduling", table => table.HasCheckConstraint("CK_CourseSectionSchedules_Time", "[StartTime] < [EndTime]"));

            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).ValueGeneratedNever();

            builder.Ignore(m => m.DomainEvents);

            builder.Property(m => m.DayOfWeek).IsRequired();
            builder.Property(m => m.StartTime).IsRequired();
            builder.Property(m => m.EndTime).IsRequired();
            builder.Property(m => m.CreatedAt).IsRequired();

            builder.HasOne<CourseSection>().WithMany().HasForeignKey(m => m.CourseSectionId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Schedule>().WithMany().HasForeignKey(m => m.ScheduleId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ScheduleSlot>().WithMany().HasForeignKey(m => m.ScheduleSlotId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Room>().WithMany().HasForeignKey(m => m.RoomId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<AcademicSemester>().WithMany().HasForeignKey(m => m.AcademicSemesterId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            // A section is placed once per slot.
            builder.HasIndex(m => new { m.CourseSectionId, m.ScheduleSlotId }).IsUnique();

            // Database backstop of ROOM_CONFLICT: the same room cannot start two meetings at the same time. Partial
            // overlaps are caught by the application check.
            builder.HasIndex(m => new { m.RoomId, m.AcademicSemesterId, m.DayOfWeek, m.StartTime }).IsUnique();

            builder.HasIndex(m => new { m.AcademicSemesterId, m.DayOfWeek });
            builder.HasIndex(m => m.ScheduleId);
        }
    }
}
