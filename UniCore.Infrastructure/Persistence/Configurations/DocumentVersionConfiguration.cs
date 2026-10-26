using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Documents;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
    {
        public void Configure(EntityTypeBuilder<DocumentVersion> builder)
        {
            builder.ToTable("DocumentVersions", "documents");

            builder.HasKey(v => v.Id);
            builder.Property(v => v.Id).ValueGeneratedNever();

            builder.Property(v => v.FileName).IsRequired().HasMaxLength(Document.MaxFileNameLength);
            builder.Property(v => v.ContentType).IsRequired().HasMaxLength(Document.MaxContentTypeLength);
            builder.Property(v => v.StorageKey).IsRequired().HasMaxLength(Document.StorageKeyLength);
            builder.Property(v => v.Checksum).IsRequired().HasMaxLength(Document.ChecksumLength);
            builder.Property(v => v.CreatedAt).IsRequired();

            builder.HasIndex(v => new { v.DocumentId, v.VersionNumber }).IsUnique();
            builder.HasIndex(v => v.StorageKey).IsUnique();
        }
    }


}
