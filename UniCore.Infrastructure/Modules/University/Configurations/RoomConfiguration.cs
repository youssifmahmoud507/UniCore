using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Modules.University;

namespace UniCore.Infrastructure.Modules.University.Configurations
{
    public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
    {
        public void Configure(EntityTypeBuilder<Room> builder)
        {
            // RoomType is stored as text with max length 50 by the convention in AppDbContext.
            // A database check keeps capacity sane even if someone bypasses the domain.
            builder.ToTable("Rooms", "university", table =>
                table.HasCheckConstraint("CK_Rooms_Capacity", "[Capacity] >= 1 AND [Capacity] <= 10000"));

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Ignore(r => r.DomainEvents);

            builder.Property(r => r.RoomNumber).IsRequired().HasMaxLength(Room.RoomNumberMaxLength);
            builder.Property(r => r.Name).HasMaxLength(FieldRules.NameMaxLength);

            // UQ(BuildingId, RoomNumber)
            builder.HasIndex(r => new { r.BuildingId, r.RoomNumber }).IsUnique();

            builder.HasOne<Building>().WithMany().HasForeignKey(r => r.BuildingId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
