using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Documents;
using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
    {
        public void Configure(EntityTypeBuilder<Document> builder)
        {
            builder.ToTable("Documents", "documents");

            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).ValueGeneratedNever();

            builder.Property(d => d.FileName).IsRequired().HasMaxLength(Document.MaxFileNameLength);
            builder.Property(d => d.ContentType).IsRequired().HasMaxLength(Document.MaxContentTypeLength);
            builder.Property(d => d.Status).IsRequired();
            builder.Property(d => d.CreatedAt).IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(d => d.DomainEvents);
            builder.Ignore(d => d.Current);

            builder.HasOne<DocumentType>().WithMany().HasForeignKey(d => d.DocumentTypeId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.OwnerUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            // The versions belong to the document.
            builder.HasMany(d => d.Versions).WithOne().HasForeignKey(v => v.DocumentId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(d => d.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasIndex(d => new { d.OwnerUserId, d.Status });
            builder.HasIndex(d => d.DocumentTypeId);
        }
    }


}
