using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RecruiterAi.Domain.Entities;

namespace RecruiterAi.Infrastructure.Persistence.Configurations;

public class CvGenerationBatchConfiguration : IEntityTypeConfiguration<CvGenerationBatch>
{
    public void Configure(EntityTypeBuilder<CvGenerationBatch> b)
    {
        b.ToTable("cv_generation_batches");
        b.HasKey(x => x.Id);

        b.Property(x => x.RequestedCount).IsRequired();
        b.Property(x => x.InferredRequirements).HasColumnType("jsonb");

        // Value converter required for InMemory provider — Dictionary<K,V> is not
        // a primitive collection, so EF Core has no built-in mapping for it.
        // ValueComparer required so EF Core change-tracking can detect in-memory mutations;
        // without it, SaveChanges() silently drops dictionary edits on tracked entities.
        b.Property(x => x.CandidateTypes)
            .HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<Dictionary<string, int>, string>(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, int>>(v, (JsonSerializerOptions?)null) ?? new()),
                new ValueComparer<Dictionary<string, int>>(
                    (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) ==
                              JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
                    v => JsonSerializer.Deserialize<Dictionary<string, int>>(
                             JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                             (JsonSerializerOptions?)null) ?? new()));
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.Position)
            .WithMany(p => p.GenerationBatches)
            .HasForeignKey(x => x.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.PositionId);
    }
}
