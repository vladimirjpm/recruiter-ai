using Microsoft.EntityFrameworkCore;
using RecruiterAi.Domain.Entities;

namespace RecruiterAi.Infrastructure.Persistence;

/// <summary>
/// Application DbContext.
///
/// DI registration:
///   builder.Services.AddDbContext&lt;AppDbContext&gt;(opt =&gt; opt.UseNpgsql(...))
///
/// All entities are mapped through IEntityTypeConfiguration classes under
/// ./Configurations/. The pgvector extension is created by the first migration
/// (CREATE EXTENSION IF NOT EXISTS vector), but the vector(1536) column on
/// CandidateSection.Embedding is only activated in Phase 2.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<CvGenerationBatch> CvGenerationBatches => Set<CvGenerationBatch>();
    public DbSet<CandidateSection> CandidateSections => Set<CandidateSection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Pick up every IEntityTypeConfiguration in this assembly.
        // Equivalent to registering each configuration explicitly one by one.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
