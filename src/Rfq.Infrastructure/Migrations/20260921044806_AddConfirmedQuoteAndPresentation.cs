using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rfq.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConfirmedQuoteAndPresentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "default_quote_expiry_minutes",
                table: "master_users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "current_quote_id",
                table: "case_currents",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE master_users SET default_quote_expiry_minutes = 5 WHERE user_id = 'trader-a';");

            migrationBuilder.CreateTable(
                name: "confirmed_quotes",
                columns: table => new
                {
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    security_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    settlement_date = table.Column<DateOnly>(type: "date", nullable: false),
                    confirmed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    calculated_payload = table.Column<string>(type: "jsonb", nullable: true),
                    manual_payload = table.Column<string>(type: "jsonb", nullable: true),
                    expiry_minutes = table.Column<int>(type: "integer", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    request_reason_answered = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_confirmed_quotes", x => x.quote_id);
                    table.ForeignKey(
                        name: "FK_confirmed_quotes_rfq_revisions_revision_id",
                        column: x => x.revision_id,
                        principalTable: "rfq_revisions",
                        principalColumn: "revision_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_case_currents_current_quote_id",
                table: "case_currents",
                column: "current_quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_confirmed_quotes_revision_id_confirmed_at",
                table: "confirmed_quotes",
                columns: new[] { "revision_id", "confirmed_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_case_currents_confirmed_quotes_current_quote_id",
                table: "case_currents",
                column: "current_quote_id",
                principalTable: "confirmed_quotes",
                principalColumn: "quote_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_case_currents_confirmed_quotes_current_quote_id",
                table: "case_currents");

            migrationBuilder.DropTable(
                name: "confirmed_quotes");

            migrationBuilder.DropIndex(
                name: "IX_case_currents_current_quote_id",
                table: "case_currents");

            migrationBuilder.DropColumn(
                name: "default_quote_expiry_minutes",
                table: "master_users");

            migrationBuilder.DropColumn(
                name: "current_quote_id",
                table: "case_currents");
        }
    }
}
