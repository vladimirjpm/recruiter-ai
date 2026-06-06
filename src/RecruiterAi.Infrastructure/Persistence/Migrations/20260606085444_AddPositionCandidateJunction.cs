using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruiterAi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPositionCandidateJunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "position_candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceContext = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_position_candidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_position_candidates_candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_position_candidates_positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_position_candidates_CandidateId",
                table: "position_candidates",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_position_candidates_PositionId",
                table: "position_candidates",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_position_candidates_PositionId_CandidateId",
                table: "position_candidates",
                columns: new[] { "PositionId", "CandidateId" },
                unique: true);

            // Backfill: every distinct (positionId, candidateId) pair that already exists
            // in evaluations becomes a ManuallyAttached junction row. Without this, demo
            // data on the deployed DB would disappear from the new position-scoped UI.
            // Uses gen_random_uuid() (pgcrypto, available by default on Postgres 13+).
            migrationBuilder.Sql(@"
                INSERT INTO position_candidates (""Id"", ""PositionId"", ""CandidateId"", ""SourceContext"", ""CreatedAt"")
                SELECT gen_random_uuid(), e.""PositionId"", e.""CandidateId"", 'ManuallyAttached', now()
                FROM evaluations e
                GROUP BY e.""PositionId"", e.""CandidateId""
                ON CONFLICT (""PositionId"", ""CandidateId"") DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "position_candidates");
        }
    }
}
