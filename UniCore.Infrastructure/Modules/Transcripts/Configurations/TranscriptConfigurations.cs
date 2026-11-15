using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Transcripts;

namespace UniCore.Infrastructure.Modules.Transcripts.Configurations
{
    public sealed class TranscriptSnapshotConfiguration : IEntityTypeConfiguration<TranscriptSnapshot>
    {
        public void Configure(EntityTypeBuilder<TranscriptSnapshot> builder)
        {
            builder.ToTable("TranscriptSnapshots", "transcript", table =>
            {
                table.HasCheckConstraint("CK_TranscriptSnapshots_Gpa", "[CumulativeGpa] >= 0 AND [CumulativeGpa] <= 5");
                table.HasCheckConstraint("CK_TranscriptSnapshots_Credits", "[AttemptedCredits] >= 0 AND [EarnedCredits] >= 0 AND [EarnedCredits] <= [AttemptedCredits]");
            });

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();

            builder.Ignore(s => s.DomainEvents);

            builder.Property(s => s.StudentId).IsRequired();
            builder.Property(s => s.StudentNumber).IsRequired().HasMaxLength(TranscriptSnapshot.StudentNumberMaxLength);
            builder.Property(s => s.VerificationCode).IsRequired().HasMaxLength(TranscriptSnapshot.VerificationCodeLength);
            builder.Property(s => s.IssuedByUserId).IsRequired();
            builder.Property(s => s.IssuedAt).IsRequired();
            builder.Property(s => s.RepeatPolicy).IsRequired().HasMaxLength(30);
            builder.Property(s => s.CumulativeGpa).IsRequired().HasPrecision(4, 2);
            builder.Property(s => s.AttemptedCredits).IsRequired();
            builder.Property(s => s.EarnedCredits).IsRequired();
            builder.Property(s => s.Standing).HasMaxLength(30);

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.HasMany(s => s.Entries).WithOne().HasForeignKey(e => e.TranscriptSnapshotId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(s => s.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);

            // The code on the paper identifies exactly one snapshot.
            builder.HasIndex(s => s.VerificationCode).IsUnique();
            builder.HasIndex(s => new { s.StudentId, s.IssuedAt });
        }
    }

    public sealed class TranscriptEntryConfiguration : IEntityTypeConfiguration<TranscriptEntry>
    {
        public void Configure(EntityTypeBuilder<TranscriptEntry> builder)
        {
            builder.ToTable("TranscriptEntries", "transcript", table =>
            {
                table.HasCheckConstraint("CK_TranscriptEntries_Credits", "[Credits] >= 0");
                table.HasCheckConstraint("CK_TranscriptEntries_Percentage", "[Percentage] >= 0 AND [Percentage] <= 100");
            });

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Property(e => e.SequenceNo).IsRequired();
            builder.Property(e => e.SemesterLabel).IsRequired().HasMaxLength(150);
            builder.Property(e => e.CourseCode).IsRequired().HasMaxLength(50);
            builder.Property(e => e.CourseTitle).IsRequired().HasMaxLength(250);
            builder.Property(e => e.Letter).IsRequired().HasMaxLength(5);
            builder.Property(e => e.Percentage).IsRequired().HasPrecision(5, 2);
            builder.Property(e => e.GradePoints).IsRequired().HasPrecision(3, 2);
            builder.Property(e => e.SemesterGpa).IsRequired().HasPrecision(4, 2);
            builder.Property(e => e.CumulativeGpa).IsRequired().HasPrecision(4, 2);

            builder.HasIndex(e => new { e.TranscriptSnapshotId, e.SequenceNo }).IsUnique();
        }
    }
}
