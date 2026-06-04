using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RecruiterAi.Domain.Entities;

namespace RecruiterAi.Infrastructure.Persistence.Configurations;

public class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> b)
    {
        b.ToTable("candidates");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).IsRequired().HasMaxLength(300);
        b.Property(x => x.Email).HasMaxLength(300);
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.FileName).IsRequired().HasMaxLength(500);
        b.Property(x => x.StoragePath).HasMaxLength(1000);
        b.Property(x => x.RawText).IsRequired();
        b.Property(x => x.Language).IsRequired().HasMaxLength(8);

        // Store enum as string — readable in the database and stable against
        // reordering enum members in code.
        b.Property(x => x.Source)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        b.Property(x => x.ExpectedFitLevel).HasMaxLength(50);

        // ScoreRange → jsonb. Value converter is required so both Npgsql (jsonb column)
        // and the InMemory provider (used in tests) know how to serialize this type.
        b.Property(x => x.ExpectedScoreRange)
            .HasColumnType("jsonb")
            .HasConversion(new ValueConverter<ScoreRange?, string?>(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<ScoreRange>(v, (JsonSerializerOptions?)null)));

        b.Property(x => x.UploadedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => x.Source);
        // Chronological listings — ORDER BY uploaded_at DESC is the default sort for candidate lists.
        b.HasIndex(x => x.UploadedAt);

        // Explicit FK replaces the shadow property EF Core would generate by convention.
        // Needed so Stage 2 code can set candidate.CvGenerationBatchId directly.
        // SetNull: deleting a batch does not delete its generated candidates.
        b.HasOne(x => x.CvGenerationBatch)
            .WithMany(b => b.GeneratedCandidates)
            .HasForeignKey(x => x.CvGenerationBatchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
