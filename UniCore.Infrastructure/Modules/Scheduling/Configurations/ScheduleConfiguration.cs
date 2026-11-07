using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Scheduling;

namespace UniCore.Infrastructure.Modules.Scheduling.Configurations
{
    public sealed class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
    {
        public void Configure(EntityTypeBuilder<Schedule> builder)
        {
            builder.ToTable("Schedules", "scheduling");

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();

            builder.Ignore(s => s.DomainEvents);
            builder.Ignore(s => s.CanChange);

            builder.Property(s => s.Name).IsRequired().HasMaxLength(FieldRules.NameMaxLength);

            // Status is stored as text (max 50) by the convention in AppDbContext.
            builder.Property(s => s.Status).IsRequired();
            builder.Property(s => s.CreatedAt).IsRequired();

            // Every placement touches the schedule, so concurrent placements fail with a concurrency conflict.
            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.HasOne<AcademicSemester>().WithMany().HasForeignKey(s => s.AcademicSemesterId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(s => new { s.AcademicSemesterId, s.Name }).IsUnique();

            builder.HasMany(s => s.Slots).WithOne().HasForeignKey(x => x.ScheduleId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(s => s.Slots).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public sealed class ScheduleSlotConfiguration : IEntityTypeConfiguration<ScheduleSlot>
    {
        public void Configure(EntityTypeBuilder<ScheduleSlot> builder)
        {
            builder.ToTable("ScheduleSlots", "scheduling", table => table.HasCheckConstraint("CK_ScheduleSlots_Time", "[StartTime] < [EndTime]"));

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();

            builder.Property(s => s.DayOfWeek).IsRequired();
            builder.Property(s => s.StartTime).IsRequired();
            builder.Property(s => s.EndTime).IsRequired();

            builder.HasIndex(s => new { s.ScheduleId, s.DayOfWeek, s.StartTime, s.EndTime }).IsUnique();
        }
    }
}
