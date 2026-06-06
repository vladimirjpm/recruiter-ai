using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruiterAi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationInputHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InputHash",
                table: "evaluations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InputHash",
                table: "evaluations");
        }
    }
}
