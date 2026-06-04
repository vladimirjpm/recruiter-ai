using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruiterAi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_evaluations_PositionId_Score",
                table: "evaluations");

            migrationBuilder.CreateIndex(
                name: "IX_evaluations_PositionId_Score",
                table: "evaluations",
                columns: new[] { "PositionId", "Score" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_candidates_UploadedAt",
                table: "candidates",
                column: "UploadedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_evaluations_PositionId_Score",
                table: "evaluations");

            migrationBuilder.DropIndex(
                name: "IX_candidates_UploadedAt",
                table: "candidates");

            migrationBuilder.CreateIndex(
                name: "IX_evaluations_PositionId_Score",
                table: "evaluations",
                columns: new[] { "PositionId", "Score" });
        }
    }
}
