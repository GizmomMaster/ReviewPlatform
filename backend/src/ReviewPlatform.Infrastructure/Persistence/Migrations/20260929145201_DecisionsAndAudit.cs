using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReviewPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DecisionsAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assessment_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    new_grade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    decided_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_decisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_assessment_decisions_assessment_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "assessment_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_assessment_decisions_grades_new_grade_id",
                        column: x => x.new_grade_id,
                        principalTable: "grades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assessment_decisions_users_decided_by_user_id",
                        column: x => x.decided_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "development_plan_item",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_indicator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_development_plan_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_development_plan_item_assessment_decisions_decision_id",
                        column: x => x.decision_id,
                        principalTable: "assessment_decisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_development_plan_item_session_indicators_session_indicator_",
                        column: x => x.session_indicator_id,
                        principalTable: "session_indicators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assessment_decisions_decided_by_user_id",
                table: "assessment_decisions",
                column: "decided_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_decisions_new_grade_id",
                table: "assessment_decisions",
                column: "new_grade_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_decisions_session_id",
                table: "assessment_decisions",
                column: "session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entity_type_entity_id_occurred_at_utc",
                table: "audit_log",
                columns: new[] { "entity_type", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_development_plan_item_decision_id",
                table: "development_plan_item",
                column: "decision_id");

            migrationBuilder.CreateIndex(
                name: "ix_development_plan_item_session_indicator_id",
                table: "development_plan_item",
                column: "session_indicator_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "development_plan_item");

            migrationBuilder.DropTable(
                name: "assessment_decisions");
        }
    }
}
