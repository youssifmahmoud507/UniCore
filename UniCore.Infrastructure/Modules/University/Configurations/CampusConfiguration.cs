using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Common.ValueObjects;
using UniCore.Domain.Modules.University;

using UniversityEntity = UniCore.Domain.Modules.University.University;

namespace UniCore.Infrastructure.Modules.University.Configurations
{
    public sealed class CampusConfiguration : IEntityTypeConfiguration<Campus>
    {
        public void Configure(EntityTypeBuilder<Campus> builder)
        {
            builder.ToTable("Campuses", "university");

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();

            builder.Ignore(c => c.DomainEvents);

            builder.Property(c => c.Code).IsRequired().HasMaxLength(FieldRules.CodeMaxLength);
            builder.Property(c => c.Name).IsRequired().HasMaxLength(FieldRules.NameMaxLength);
            builder.Property(c => c.TimeZone).IsRequired().HasMaxLength(FieldRules.TimeZoneMaxLength);

            // The address is a value object stored as columns of this table.
            builder.OwnsOne(c => c.Address, address =>
            {
                address.Property(a => a.Street).HasColumnName("AddressStreet").IsRequired().HasMaxLength(Address.StreetMaxLength);
                address.Property(a => a.City).HasColumnName("AddressCity").IsRequired().HasMaxLength(Address.CityMaxLength);
                address.Property(a => a.State).HasColumnName("AddressState").HasMaxLength(Address.StateMaxLength);
                address.Property(a => a.PostalCode).HasColumnName("AddressPostalCode").HasMaxLength(Address.PostalCodeMaxLength);
                address.Property(a => a.Country).HasColumnName("AddressCountry").IsRequired().HasMaxLength(Address.CountryMaxLength);
            });
            builder.Navigation(c => c.Address).IsRequired();

            // UQ(UniversityId, Code)
            builder.HasIndex(c => new { c.UniversityId, c.Code }).IsUnique();

            // Records are deactivated, never deleted, so no cascade.
            builder.HasOne<UniversityEntity>().WithMany().HasForeignKey(c => c.UniversityId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
