using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Documents;
using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class DocumentAccessConfiguration : IEntityTypeConfiguration<DocumentAccess>
    {
        public void Configure(EntityTypeBuilder<DocumentAccess> builder)
        {
            builder.ToTable("DocumentAccesses", "documents");

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();

            builder.Property(a => a.AccessType).IsRequired();
            builder.Property(a => a.Status).IsRequired();
            builder.Property(a => a.GrantedAt).IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(a => a.DomainEvents);

            builder.HasOne<Document>().WithMany().HasForeignKey(a => a.DocumentId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.UserId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            // One active grant per (document, person). Revoked ones are history.
            builder.HasIndex(a => new { a.DocumentId, a.UserId })
                .IsUnique()
                .HasFilter("[Status] = 'Active'")
                .HasDatabaseName("UX_DocumentAccesses_Active");

            builder.HasIndex(a => a.UserId);
        }
    }


}
