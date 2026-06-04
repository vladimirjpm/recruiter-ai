using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RecruiterAi.Domain.Entities;

namespace RecruiterAi.Infrastructure.Persistence.Configurations;

public class EvaluationConfiguration : IEntityTypeConfiguration<Evaluation>
{
    public void Configure(EntityTypeBuilder<Evaluation> b)
    {
        b.ToTable("evaluations");
        b.HasKey(x => x.Id);

        b.Property(x => x.Score).IsRequired();
        b.Property(x => x.MatchLevel)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        b.Property(x => x.Reasoning).IsRequired();
        b.Property(x => x.Strengths).HasColumnType("jsonb");
        b.Property(x => x.Weaknesses).HasColumnType("jsonb");
        b.Property(x => x.MatchedSkills).HasColumnType("jsonb");
        b.Property(x => x.MissingSkills).HasColumnType("jsonb");
        b.Property(x => x.RedFlags).HasColumnType("jsonb");
        b.Property(x => x.InterviewQuestions).HasColumnType("jsonb");

        b.Property(x => x.AiModel).IsRequired().HasMaxLength(100);

        // Audit / reproducibility — required on every evaluation
        b.Property(x => x.PromptVersion).IsRequired().HasMaxLength(50);
        b.Property(x => x.SchemaVersion).IsRequired().HasMaxLength(50);
        // decimal(4,3): values in [0.000 … 9.999] — covers all valid temperature ranges
        b.Property(x => x.Temperature).HasColumnType("decimal(4,3)").IsRequired();
        b.Property(x => x.EvaluationDurationMs).IsRequired();

        // Cost tracking — nullable until OpenAI client is wired (Stage 4)
        // Value converter + ValueComparer required for InMemory provider and EF change-tracking.
        // Without ValueComparer, SaveChanges() silently drops mutations on tracked nullable dicts.
        b.Property(x => x.ScoreBreakdown)
            .HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<Dictionary<string, int>?, string?>(
                    v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, int>>(v, (JsonSerializerOptions?)null)),
                new ValueComparer<Dictionary<string, int>?>(
                    (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) ==
                              JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                    v => v == null ? 0 : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
                    v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, int>>(
                             JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                             (JsonSerializerOptions?)null)));
        b.Property(x => x.InputTokens);
        b.Property(x => x.OutputTokens);
        // decimal(10,6): up to $9999.999999 per call — sufficient for any realistic model pricing
        b.Property(x => x.EstimatedCost).HasColumnType("decimal(10,6)");

        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.Candidate)
            .WithMany(c => c.Evaluations)
            .HasForeignKey(x => x.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Position)
            .WithMany(p => p.Evaluations)
            .HasForeignKey(x => x.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Score DESC — results page sorts best matches first within a position.
        b.HasIndex(x => new { x.PositionId, x.Score }).IsDescending(false, true);
        b.HasIndex(x => x.CandidateId);
    }
}
