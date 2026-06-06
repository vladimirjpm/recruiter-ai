using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecruiterAi.Domain.Entities;

namespace RecruiterAi.Infrastructure.Persistence.Configurations;

public class PositionCandidateConfiguration : IEntityTypeConfiguration<PositionCandidate>
{
    public void Configure(EntityTypeBuilder<PositionCandidate> b)
    {
        b.ToTable("position_candidates");
        b.HasKey(x => x.Id);

        // String column for enum — stable across renames and easy to read in psql.
        b.Property(x => x.SourceContext)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(32);

        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        // Recruiter override: default 0, comment capped to keep audit readable.
        b.Property(x => x.RecruiterAdjustment).IsRequired().HasDefaultValue(0);
        b.Property(x => x.RecruiterComment).HasMaxLength(1000);
        b.Property(x => x.AdjustedBy).HasMaxLength(200);

        b.HasOne(x => x.Position)
            .WithMany(p => p.PositionCandidates)
            .HasForeignKey(x => x.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Candidate)
            .WithMany(c => c.PositionCandidates)
            .HasForeignKey(x => x.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        // Idempotent attach: one row per (position, candidate).
        b.HasIndex(x => new { x.PositionId, x.CandidateId }).IsUnique();

        // Frequent lookup: candidates for a position.
        b.HasIndex(x => x.PositionId);
    }
}
