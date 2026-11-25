using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.StudentServices;

namespace UniCore.Infrastructure.Modules.StudentServices.Configurations
{
    // The shared columns of every request kind. ServiceRequest itself is not an entity: each kind has its own table in schema "studentservices".
    internal static class ServiceRequestColumns
    {
        public static void Map<T>(EntityTypeBuilder<T> builder, string table) where T : ServiceRequest
        {
            builder.ToTable(table, "studentservices");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Ignore(r => r.DomainEvents);
            builder.Ignore(r => r.IsOpen);

            builder.Property(r => r.StudentId).IsRequired();
            builder.Property(r => r.RequestedByUserId).IsRequired();

            // Status is stored as text by the convention in AppDbContext.
            builder.Property(r => r.Status).IsRequired();
            builder.Property(r => r.SubmittedAt).IsRequired();
            builder.Property(r => r.UpdatedAt).IsRequired();
            builder.Property(r => r.DecisionNote).HasMaxLength(ServiceRequest.NoteMaxLength);

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.HasIndex(r => new { r.StudentId, r.SubmittedAt });
            builder.HasIndex(r => new { r.Status, r.SubmittedAt });
            builder.HasIndex(r => r.WorkflowInstanceId);
        }
    }

    public sealed class CertificateRequestConfiguration : IEntityTypeConfiguration<CertificateRequest>
    {
        public void Configure(EntityTypeBuilder<CertificateRequest> builder)
        {
            ServiceRequestColumns.Map(builder, "CertificateRequests");

            builder.Property(r => r.CertificateType).IsRequired();
            builder.Property(r => r.Purpose).IsRequired().HasMaxLength(ServiceRequest.ShortTextMaxLength);

            // One open request per student and certificate type: two requests at the same moment cannot both succeed.
            builder.HasIndex(r => new { r.StudentId, r.CertificateType }).IsUnique().HasDatabaseName("UX_CertificateRequests_OneOpenPerStudentAndType")
                .HasFilter("[Status] IN ('Pending', 'Returned')");
        }
    }

    public sealed class TranscriptRequestConfiguration : IEntityTypeConfiguration<TranscriptRequest>
    {
        public void Configure(EntityTypeBuilder<TranscriptRequest> builder)
        {
            ServiceRequestColumns.Map(builder, "TranscriptRequests");

            builder.Ignore(r => r.IsIssued);
            builder.Property(r => r.Purpose).IsRequired().HasMaxLength(ServiceRequest.ShortTextMaxLength);

            builder.HasIndex(r => r.StudentId).IsUnique().HasDatabaseName("UX_TranscriptRequests_OneOpenPerStudent")
                .HasFilter("[Status] IN ('Pending', 'Returned')");
        }
    }

    public sealed class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
    {
        public void Configure(EntityTypeBuilder<Complaint> builder)
        {
            ServiceRequestColumns.Map(builder, "Complaints");

            builder.Property(r => r.Category).IsRequired();
            builder.Property(r => r.Subject).IsRequired().HasMaxLength(ServiceRequest.ShortTextMaxLength);
            builder.Property(r => r.Details).IsRequired().HasMaxLength(ServiceRequest.TextMaxLength);
        }
    }

    public sealed class AppealConfiguration : IEntityTypeConfiguration<Appeal>
    {
        public void Configure(EntityTypeBuilder<Appeal> builder)
        {
            ServiceRequestColumns.Map(builder, "Appeals");

            builder.Property(r => r.Target).IsRequired();
            builder.Property(r => r.Subject).IsRequired().HasMaxLength(ServiceRequest.ShortTextMaxLength);
            builder.Property(r => r.Reason).IsRequired().HasMaxLength(ServiceRequest.TextMaxLength);
        }
    }
}
