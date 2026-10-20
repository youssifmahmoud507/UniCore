using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Modules.Admissions;

namespace UniCore.Infrastructure.Modules.Admissions.Configurations
{
    public sealed class ApplicantConfiguration : IEntityTypeConfiguration<Applicant>
    {
        public void Configure(EntityTypeBuilder<Applicant> builder)
        {
            builder.ToTable("Applicants", "admissions");

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();

            builder.Ignore(a => a.DomainEvents);

            builder.Property(a => a.FullName).IsRequired().HasMaxLength(FieldRules.NameMaxLength);
            builder.Property(a => a.Email).IsRequired().HasMaxLength(FieldRules.EmailMaxLength);
            builder.Property(a => a.Phone).HasMaxLength(FieldRules.PhoneMaxLength);
            builder.Property(a => a.DateOfBirth).IsRequired();
            builder.Property(a => a.CreatedAt).IsRequired();

            // One applicant per email (the domain lower-cases it).
            builder.HasIndex(a => a.Email).IsUnique();

            // At most one applicant per login account; applicants without an account (NULL) are not limited.
            builder.HasIndex(a => a.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
        }
    }
}
