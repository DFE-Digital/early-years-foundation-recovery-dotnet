using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PageSpecificLearningNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LearningNotes_UserAccountId_ModuleName",
                table: "LearningNotes");

            migrationBuilder.AddColumn<string>(
                name: "PageName",
                table: "LearningNotes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "LearningNotes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_LearningNotes_UserAccountId_ModuleName_PageName",
                table: "LearningNotes",
                columns: new[] { "UserAccountId", "ModuleName", "PageName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LearningNotes_UserAccountId_ModuleName_PageName",
                table: "LearningNotes");

            migrationBuilder.DropColumn(
                name: "PageName",
                table: "LearningNotes");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "LearningNotes");

            migrationBuilder.CreateIndex(
                name: "IX_LearningNotes_UserAccountId_ModuleName",
                table: "LearningNotes",
                columns: new[] { "UserAccountId", "ModuleName" },
                unique: true);
        }
    }
}
