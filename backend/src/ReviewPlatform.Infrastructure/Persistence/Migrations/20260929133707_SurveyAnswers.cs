using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReviewPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SurveyAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_submission_at_utc",
                table: "assessment_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "survey_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_indicator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: true),
                    not_applicable = table.Column<bool>(type: "boolean", nullable: false),
                    comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_answers", x => x.id);
                    table.ForeignKey(
                        name: "fk_survey_answers_participants_participant_id",
                        column: x => x.participant_id,
                        principalTable: "participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_survey_answers_session_indicators_session_indicator_id",
                        column: x => x.session_indicator_id,
                        principalTable: "session_indicators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_survey_answers_participant_id_session_indicator_id",
                table: "survey_answers",
                columns: new[] { "participant_id", "session_indicator_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_survey_answers_session_indicator_id",
                table: "survey_answers",
                column: "session_indicator_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "survey_answers");

            migrationBuilder.DropColumn(
                name: "last_submission_at_utc",
                table: "assessment_sessions");
        }
    }
}
