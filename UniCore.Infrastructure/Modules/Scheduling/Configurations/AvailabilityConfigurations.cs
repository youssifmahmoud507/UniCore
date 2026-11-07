using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.People;
using UniCore.Domain.Modules.Scheduling;
using UniCore.Domain.Modules.University;

namespace UniCore.Infrastructure.Modules.Scheduling.Configurations
{
    public sealed class InstructorAvailabilityConfiguration : IEntityTypeConfiguration<InstructorAvailability>
    {
        public void Configure(EntityTypeBuilder<InstructorAvailability> builder)
        {
            builder.ToTable("InstructorAvailabilities", "scheduling", table => table.HasCheckConstraint("CK_InstructorAvailabilities_Time", "[StartTime] < [EndTime]"));

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();

            builder.Ignore(a => a.DomainEvents);

            builder.Property(a => a.DayOfWeek).IsRequired();
            builder.Property(a => a.StartTime).IsRequired();
            builder.Property(a => a.EndTime).IsRequired();

            builder.HasOne<Instructor>().WithMany().HasForeignKey(a => a.InstructorId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(a => new { a.InstructorId, a.DayOfWeek, a.StartTime }).IsUnique();
        }
    }

    public sealed class RoomAvailabilityConfiguration : IEntityTypeConfiguration<RoomAvailability>
    {
        public void Configure(EntityTypeBuilder<RoomAvailability> builder)
        {
            builder.ToTable("RoomAvailabilities", "scheduling", table => table.HasCheckConstraint("CK_RoomAvailabilities_Time", "[StartTime] < [EndTime]"));

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();

            builder.Ignore(a => a.DomainEvents);

            builder.Property(a => a.DayOfWeek).IsRequired();
            builder.Property(a => a.StartTime).IsRequired();
            builder.Property(a => a.EndTime).IsRequired();

            builder.HasOne<Room>().WithMany().HasForeignKey(a => a.RoomId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(a => new { a.RoomId, a.DayOfWeek, a.StartTime }).IsUnique();
        }
    }
}
