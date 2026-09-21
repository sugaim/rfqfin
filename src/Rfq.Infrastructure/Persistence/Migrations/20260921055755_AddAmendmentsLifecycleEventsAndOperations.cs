using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rfq.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAmendmentsLifecycleEventsAndOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "copied_from_revision_id",
                table: "rfq_revisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "copied_from_case_id",
                table: "rfq_cases",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "event_cursors",
                columns: table => new
                {
                    cursor_key = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    last_event_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_cursors", x => x.cursor_key);
                });

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "user_grid_configs",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    screen_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    config_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    config_json = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_grid_configs", x => new { x.user_id, x.screen_id, x.config_key });
                });

            migrationBuilder.CreateTable(
                name: "quote_events",
                columns: table => new
                {
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    case_id = table.Column<long>(type: "bigint", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_events", x => x.event_id);
                    table.ForeignKey(
                        name: "FK_quote_events_confirmed_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "confirmed_quotes",
                        principalColumn: "quote_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_quote_events_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "event_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_quote_events_rfq_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "rfq_cases",
                        principalColumn: "case_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rfq_events",
                columns: table => new
                {
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    case_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rfq_events", x => x.event_id);
                    table.ForeignKey(
                        name: "FK_rfq_events_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "event_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rfq_events_rfq_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "rfq_cases",
                        principalColumn: "case_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "event_cursors",
                columns: new[] { "cursor_key", "last_event_id" },
                values: new object[] { "global", 0L });

            migrationBuilder.CreateIndex(
                name: "IX_rfq_revisions_copied_from_revision_id",
                table: "rfq_revisions",
                column: "copied_from_revision_id");

            migrationBuilder.CreateIndex(
                name: "IX_rfq_cases_copied_from_case_id",
                table: "rfq_cases",
                column: "copied_from_case_id");

            migrationBuilder.CreateIndex(
                name: "IX_quote_events_case_id",
                table: "quote_events",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "IX_quote_events_quote_id",
                table: "quote_events",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "IX_rfq_events_case_id",
                table: "rfq_events",
                column: "case_id");

            migrationBuilder.AddForeignKey(
                name: "FK_rfq_cases_rfq_cases_copied_from_case_id",
                table: "rfq_cases",
                column: "copied_from_case_id",
                principalTable: "rfq_cases",
                principalColumn: "case_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_rfq_revisions_rfq_revisions_copied_from_revision_id",
                table: "rfq_revisions",
                column: "copied_from_revision_id",
                principalTable: "rfq_revisions",
                principalColumn: "revision_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rfq_cases_rfq_cases_copied_from_case_id",
                table: "rfq_cases");

            migrationBuilder.DropForeignKey(
                name: "FK_rfq_revisions_rfq_revisions_copied_from_revision_id",
                table: "rfq_revisions");

            migrationBuilder.DropTable(
                name: "event_cursors");

            migrationBuilder.DropTable(
                name: "quote_events");

            migrationBuilder.DropTable(
                name: "rfq_events");

            migrationBuilder.DropTable(
                name: "user_grid_configs");

            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropIndex(
                name: "IX_rfq_revisions_copied_from_revision_id",
                table: "rfq_revisions");

            migrationBuilder.DropIndex(
                name: "IX_rfq_cases_copied_from_case_id",
                table: "rfq_cases");

            migrationBuilder.DropColumn(
                name: "copied_from_revision_id",
                table: "rfq_revisions");

            migrationBuilder.DropColumn(
                name: "copied_from_case_id",
                table: "rfq_cases");
        }
    }
}
