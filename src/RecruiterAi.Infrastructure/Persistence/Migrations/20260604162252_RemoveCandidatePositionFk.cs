using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruiterAi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCandidatePositionFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_candidates_positions_PositionId",
                table: "candidates");

            migrationBuilder.DropIndex(
                name: "IX_candidates_PositionId",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "PositionId",
                table: "candidates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PositionId",
                table: "candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidates_PositionId",
                table: "candidates",
                column: "PositionId");

            migrationBuilder.AddForeignKey(
                name: "FK_candidates_positions_PositionId",
                table: "candidates",
                column: "PositionId",
                principalTable: "positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
