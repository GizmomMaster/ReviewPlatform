using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReviewPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssessmentSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assessment_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    current_grade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_grade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    deadline_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    launched_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_assessment_sessions_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assessment_sessions_grades_current_grade_id",
                        column: x => x.current_grade_id,
                        principalTable: "grades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assessment_sessions_grades_target_grade_id",
                        column: x => x.target_grade_id,
                        principalTable: "grades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assessment_sessions_tracks_track_id",
                        column: x => x.track_id,
                        principalTable: "tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assessment_sessions_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    token_issued_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    first_opened_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_reminder_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_participants_assessment_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "assessment_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_indicators",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_indicator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    group_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    group_order = table.Column<int>(type: "integer", nullable: false),
                    grade_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    level_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_indicators", x => x.id);
                    table.ForeignKey(
                        name: "fk_session_indicators_assessment_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "assessment_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assessment_sessions_current_grade_id",
                table: "assessment_sessions",
                column: "current_grade_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_sessions_owner_user_id_status",
                table: "assessment_sessions",
                columns: new[] { "owner_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_assessment_sessions_target_grade_id",
                table: "assessment_sessions",
                column: "target_grade_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_sessions_track_id",
                table: "assessment_sessions",
                column: "track_id");

            migrationBuilder.CreateIndex(
                name: "ux_assessment_sessions_active_employee",
                table: "assessment_sessions",
                column: "employee_id",
                unique: true,
                filter: "status IN ('Draft', 'InProgress', 'Overdue', 'AwaitingDecision')");

            migrationBuilder.CreateIndex(
                name: "ix_participants_session_id",
                table: "participants",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_participants_token_hash",
                table: "participants",
                column: "token_hash",
                unique: true,
                filter: "token_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_session_indicators_session_id",
                table: "session_indicators",
                column: "session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "participants");

            migrationBuilder.DropTable(
                name: "session_indicators");

            migrationBuilder.DropTable(
                name: "assessment_sessions");
        }
    }
}
