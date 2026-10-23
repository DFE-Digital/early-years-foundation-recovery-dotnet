using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GovOneSubject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LocalAuthority = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SettingType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SettingTypeOther = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SettingRequiresLocalAuthority = table.Column<bool>(type: "boolean", nullable: false),
                    RoleGroup = table.Column<string>(type: "text", nullable: true),
                    RoleType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RoleTypeOther = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EarlyYearsExperience = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TrainingEmails = table.Column<bool>(type: "boolean", nullable: true),
                    ResearchParticipant = table.Column<bool>(type: "boolean", nullable: true),
                    TermsAndConditionsAgreedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RegistrationCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedReason = table.Column<string>(type: "text", nullable: true),
                    ClosedReasonCustom = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    ModuleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Score = table.Column<decimal>(type: "numeric", nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentAttempts_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningNotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    ModuleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningNotes_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingModuleProgress",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    ModuleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastPageName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingModuleProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingModuleProgress_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingResponses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    ModuleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    QuestionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ResponseType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    selected_answer_ids = table.Column<string>(type: "text", nullable: false),
                    Correct = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingResponses_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assessment_answers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuestionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    selected_answer_ids = table.Column<string>(type: "text", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    AssessmentAttemptId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assessment_answers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assessment_answers_AssessmentAttempts_AssessmentAttemptId",
                        column: x => x.AssessmentAttemptId,
                        principalTable: "AssessmentAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "visited_training_pages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VisitedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TrainingModuleProgressId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visited_training_pages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_visited_training_pages_TrainingModuleProgress_TrainingModul~",
                        column: x => x.TrainingModuleProgressId,
                        principalTable: "TrainingModuleProgress",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assessment_answers_AssessmentAttemptId",
                table: "assessment_answers",
                column: "AssessmentAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentAttempts_UserAccountId_ModuleName_StartedAt",
                table: "AssessmentAttempts",
                columns: new[] { "UserAccountId", "ModuleName", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningNotes_UserAccountId_ModuleName",
                table: "LearningNotes",
                columns: new[] { "UserAccountId", "ModuleName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingModuleProgress_UserAccountId_ModuleName",
                table: "TrainingModuleProgress",
                columns: new[] { "UserAccountId", "ModuleName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingResponses_UserAccountId_ModuleName_QuestionName_Cre~",
                table: "TrainingResponses",
                columns: new[] { "UserAccountId", "ModuleName", "QuestionName", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_GovOneSubject",
                table: "UserAccounts",
                column: "GovOneSubject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_visited_training_pages_TrainingModuleProgressId_Name",
                table: "visited_training_pages",
                columns: new[] { "TrainingModuleProgressId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assessment_answers");

            migrationBuilder.DropTable(
                name: "LearningNotes");

            migrationBuilder.DropTable(
                name: "TrainingResponses");

            migrationBuilder.DropTable(
                name: "visited_training_pages");

            migrationBuilder.DropTable(
                name: "AssessmentAttempts");

            migrationBuilder.DropTable(
                name: "TrainingModuleProgress");

            migrationBuilder.DropTable(
                name: "UserAccounts");
        }
    }
}
