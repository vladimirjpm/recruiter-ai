using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruiterAi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecruiterAdjustmentToPositionCandidate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AdjustedAt",
                table: "position_candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdjustedBy",
                table: "position_candidates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecruiterAdjustment",
                table: "position_candidates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RecruiterComment",
                table: "position_candidates",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdjustedAt",
                table: "position_candidates");

            migrationBuilder.DropColumn(
                name: "AdjustedBy",
                table: "position_candidates");

            migrationBuilder.DropColumn(
                name: "RecruiterAdjustment",
                table: "position_candidates");

            migrationBuilder.DropColumn(
                name: "RecruiterComment",
                table: "position_candidates");
        }
    }
}
