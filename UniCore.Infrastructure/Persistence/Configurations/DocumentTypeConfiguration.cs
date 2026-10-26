using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Documents;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
    {
        public void Configure(EntityTypeBuilder<DocumentType> builder)
        {
            builder.ToTable("DocumentTypes", "documents");

            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();

            builder.Property(t => t.Code).IsRequired().HasMaxLength(DocumentType.MaxCodeLength);
            builder.Property(t => t.Name).IsRequired().HasMaxLength(DocumentType.MaxNameLength);
            builder.Property(t => t.IsSensitive).IsRequired();
            builder.Property(t => t.AllowedFormats).IsRequired();
            builder.Property(t => t.IsActive).IsRequired();
            builder.Property(t => t.CreatedAt).IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(t => t.DomainEvents);

            builder.HasIndex(t => t.Code).IsUnique();
        }
    }


}
