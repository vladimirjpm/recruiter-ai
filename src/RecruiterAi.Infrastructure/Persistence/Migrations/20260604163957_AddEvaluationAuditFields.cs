using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruiterAi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCost",
                table: "evaluations",
                type: "numeric(10,6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EvaluationDurationMs",
                table: "evaluations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InputTokens",
                table: "evaluations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutputTokens",
                table: "evaluations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptVersion",
                table: "evaluations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SchemaVersion",
                table: "evaluations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Dictionary<string, int>>(
                name: "ScoreBreakdown",
                table: "evaluations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Temperature",
                table: "evaluations",
                type: "numeric(4,3)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedCost",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "EvaluationDurationMs",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "InputTokens",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "OutputTokens",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "PromptVersion",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "ScoreBreakdown",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "Temperature",
                table: "evaluations");
        }
    }
}
