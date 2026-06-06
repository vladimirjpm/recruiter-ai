using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruiterAi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillGeneratedCandidatesJunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The previous migration backfilled junction rows only for (position, candidate)
            // pairs that already existed in evaluations. Generated candidates that were never
            // screened were left orphaned and therefore invisible in the new position-scoped
            // Candidates page. Recover them through CvGenerationBatch.PositionId, which is
            // the authoritative target position for every generated candidate.
            //
            // ON CONFLICT DO NOTHING keeps the migration idempotent and avoids duplicating
            // rows the evaluations-based backfill already inserted (those came in with
            // SourceContext='ManuallyAttached' and the unique index protects against duplicates).
            migrationBuilder.Sql(@"
                INSERT INTO position_candidates (""Id"", ""PositionId"", ""CandidateId"", ""SourceContext"", ""CreatedAt"")
                SELECT gen_random_uuid(), b.""PositionId"", c.""Id"", 'Generated', c.""UploadedAt""
                FROM candidates c
                JOIN cv_generation_batches b ON b.""Id"" = c.""CvGenerationBatchId""
                WHERE c.""CvGenerationBatchId"" IS NOT NULL
                ON CONFLICT (""PositionId"", ""CandidateId"") DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down is a no-op: backfilled rows are indistinguishable from rows produced
            // by real Generator runs (both have SourceContext='Generated').
        }
    }
}
