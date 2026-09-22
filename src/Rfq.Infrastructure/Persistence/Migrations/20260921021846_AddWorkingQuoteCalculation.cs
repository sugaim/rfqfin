using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rfq.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddWorkingQuoteCalculation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "calculated_payload",
            table: "working_quotes",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "manual_payload",
            table: "working_quotes",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "mode",
            table: "working_quotes",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "updated_at",
            table: "working_quotes",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "updated_by",
            table: "working_quotes",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE working_quotes
            SET mode = 'Calculated',
                updated_at = created_at,
                updated_by = created_by;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "mode",
            table: "working_quotes",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(30)",
            oldMaxLength: 30,
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "updated_at",
            table: "working_quotes",
            type: "timestamp with time zone",
            nullable: false,
            oldClrType: typeof(DateTimeOffset),
            oldType: "timestamp with time zone",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "updated_by",
            table: "working_quotes",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "quote_seed_revision_id",
            table: "rfq_revisions",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "calculation_failure_logs",
            columns: table => new
            {
                failure_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                case_id = table.Column<long>(type: "bigint", nullable: false),
                revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                trader_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                request_id = table.Column<Guid>(type: "uuid", nullable: false),
                driver = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                attempted_value = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: false),
                simple_yield_slide = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: false),
                prior_working_quote = table.Column<string>(type: "jsonb", nullable: false),
                request_payload = table.Column<string>(type: "jsonb", nullable: false),
                error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                error_message = table.Column<string>(type: "text", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_calculation_failure_logs", x => x.failure_log_id);
                table.ForeignKey(
                    name: "FK_calculation_failure_logs_rfq_cases_case_id",
                    column: x => x.case_id,
                    principalTable: "rfq_cases",
                    principalColumn: "case_id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_calculation_failure_logs_rfq_revisions_revision_id",
                    column: x => x.revision_id,
                    principalTable: "rfq_revisions",
                    principalColumn: "revision_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_rfq_revisions_quote_seed_revision_id",
            table: "rfq_revisions",
            column: "quote_seed_revision_id");

        migrationBuilder.CreateIndex(
            name: "ix_calculation_failure_logs_case_id_occurred_at",
            table: "calculation_failure_logs",
            columns: ["case_id", "occurred_at"]);

        migrationBuilder.CreateIndex(
            name: "IX_calculation_failure_logs_revision_id",
            table: "calculation_failure_logs",
            column: "revision_id");

        migrationBuilder.AddForeignKey(
            name: "FK_rfq_revisions_rfq_revisions_quote_seed_revision_id",
            table: "rfq_revisions",
            column: "quote_seed_revision_id",
            principalTable: "rfq_revisions",
            principalColumn: "revision_id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_rfq_revisions_rfq_revisions_quote_seed_revision_id",
            table: "rfq_revisions");

        migrationBuilder.DropTable(
            name: "calculation_failure_logs");

        migrationBuilder.DropIndex(
            name: "IX_rfq_revisions_quote_seed_revision_id",
            table: "rfq_revisions");

        migrationBuilder.DropColumn(
            name: "calculated_payload",
            table: "working_quotes");

        migrationBuilder.DropColumn(
            name: "manual_payload",
            table: "working_quotes");

        migrationBuilder.DropColumn(
            name: "mode",
            table: "working_quotes");

        migrationBuilder.DropColumn(
            name: "updated_at",
            table: "working_quotes");

        migrationBuilder.DropColumn(
            name: "updated_by",
            table: "working_quotes");

        migrationBuilder.DropColumn(
            name: "quote_seed_revision_id",
            table: "rfq_revisions");
    }
}
