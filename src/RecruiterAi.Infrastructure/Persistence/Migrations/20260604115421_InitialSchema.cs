using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using RecruiterAi.Domain.Entities;

#nullable disable

namespace RecruiterAi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // pgvector extension — schema-ready for Phase 2.
            // Nothing uses it in Phase 1, but the pgvector/pgvector:pg16 image
            // already ships the extension, so Phase 2 will not require a
            // service-level migration to enable it.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SeniorityLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RequiredSkills = table.Column<string>(type: "jsonb", nullable: false),
                    NiceToHaveSkills = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_positions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cv_generation_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedCount = table.Column<int>(type: "integer", nullable: false),
                    InferredRequirements = table.Column<string>(type: "jsonb", nullable: false),
                    CandidateTypes = table.Column<Dictionary<string, int>>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cv_generation_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cv_generation_batches_positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Email = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RawText = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpectedFitLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExpectedScoreRange = table.Column<ScoreRange>(type: "jsonb", nullable: true),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CvGenerationBatchId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_candidates_cv_generation_batches_CvGenerationBatchId",
                        column: x => x.CvGenerationBatchId,
                        principalTable: "cv_generation_batches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_candidates_positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "candidate_sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<string>(type: "jsonb", nullable: true),
                    EmbeddingModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_candidate_sections_candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    MatchLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reasoning = table.Column<string>(type: "text", nullable: false),
                    Strengths = table.Column<string>(type: "jsonb", nullable: false),
                    Weaknesses = table.Column<string>(type: "jsonb", nullable: false),
                    MatchedSkills = table.Column<string>(type: "jsonb", nullable: false),
                    MissingSkills = table.Column<string>(type: "jsonb", nullable: false),
                    RedFlags = table.Column<string>(type: "jsonb", nullable: false),
                    InterviewQuestions = table.Column<string>(type: "jsonb", nullable: false),
                    AiModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evaluations_candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_evaluations_positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_sections_CandidateId",
                table: "candidate_sections",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_sections_SectionType",
                table: "candidate_sections",
                column: "SectionType");

            migrationBuilder.CreateIndex(
                name: "IX_candidates_CvGenerationBatchId",
                table: "candidates",
                column: "CvGenerationBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_candidates_PositionId",
                table: "candidates",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_candidates_Source",
                table: "candidates",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_cv_generation_batches_PositionId",
                table: "cv_generation_batches",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_evaluations_CandidateId",
                table: "evaluations",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_evaluations_PositionId_Score",
                table: "evaluations",
                columns: new[] { "PositionId", "Score" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_sections");

            migrationBuilder.DropTable(
                name: "evaluations");

            migrationBuilder.DropTable(
                name: "candidates");

            migrationBuilder.DropTable(
                name: "cv_generation_batches");

            migrationBuilder.DropTable(
                name: "positions");
        }
    }
}
