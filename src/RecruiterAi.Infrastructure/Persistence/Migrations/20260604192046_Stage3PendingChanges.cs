using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruiterAi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Stage3PendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_candidates_cv_generation_batches_CvGenerationBatchId",
                table: "candidates");

            migrationBuilder.AddForeignKey(
                name: "FK_candidates_cv_generation_batches_CvGenerationBatchId",
                table: "candidates",
                column: "CvGenerationBatchId",
                principalTable: "cv_generation_batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_candidates_cv_generation_batches_CvGenerationBatchId",
                table: "candidates");

            migrationBuilder.AddForeignKey(
                name: "FK_candidates_cv_generation_batches_CvGenerationBatchId",
                table: "candidates",
                column: "CvGenerationBatchId",
                principalTable: "cv_generation_batches",
                principalColumn: "Id");
        }
    }
}
