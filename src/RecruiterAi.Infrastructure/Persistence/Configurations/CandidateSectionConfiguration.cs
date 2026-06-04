using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecruiterAi.Domain.Entities;

namespace RecruiterAi.Infrastructure.Persistence.Configurations;

/// <summary>
/// Phase 1: table is created but never populated.
/// Phase 2: the embedding column type changes to vector(1536) (pgvector),
/// and an ivfflat index is added for cosine-similarity search.
///
/// For Phase 1 the embedding is modelled as a jsonb-encoded float[], so that:
///   1) we don't have to pull in Pgvector.EntityFrameworkCore yet
///      (the package did not support EF Core 10 at the time of Stage 1);
///   2) Phase 2 migrates the column type and recomputes all rows in one go.
/// </summary>
public class CandidateSectionConfiguration : IEntityTypeConfiguration<CandidateSection>
{
    public void Configure(EntityTypeBuilder<CandidateSection> b)
    {
        b.ToTable("candidate_sections");
        b.HasKey(x => x.Id);

        b.Property(x => x.SectionType).IsRequired().HasMaxLength(50);
        b.Property(x => x.Content).IsRequired();

        // Phase 1 placeholder. Phase 2 swaps this to
        // .HasColumnType("vector(1536)") via Pgvector.EntityFrameworkCore.
        b.Property(x => x.Embedding).HasColumnType("jsonb");

        b.Property(x => x.EmbeddingModel).HasMaxLength(100);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.Candidate)
            .WithMany(c => c.Sections)
            .HasForeignKey(x => x.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.CandidateId);
        b.HasIndex(x => x.SectionType);
    }
}
