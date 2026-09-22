using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rfq.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddInitialRevisionConfirm : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<DateOnly>(
            name: "settlement_date",
            table: "rfq_revisions",
            type: "date",
            nullable: true,
            oldClrType: typeof(DateOnly),
            oldType: "date");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "confirmed_at",
            table: "rfq_revisions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "confirmed_by",
            table: "rfq_revisions",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "notional",
            table: "rfq_revisions",
            type: "numeric(20,2)",
            precision: 20,
            scale: 2,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "sales_and_trading_message",
            table: "rfq_revisions",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "quote_request_reason",
            table: "case_currents",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "quote_status",
            table: "case_currents",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "working_quotes",
            columns: table => new
            {
                revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_working_quotes", x => x.revision_id);
                table.ForeignKey(
                    name: "FK_working_quotes_rfq_revisions_revision_id",
                    column: x => x.revision_id,
                    principalTable: "rfq_revisions",
                    principalColumn: "revision_id",
                    onDelete: ReferentialAction.Cascade);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "working_quotes");

        migrationBuilder.DropColumn(
            name: "confirmed_at",
            table: "rfq_revisions");

        migrationBuilder.DropColumn(
            name: "confirmed_by",
            table: "rfq_revisions");

        migrationBuilder.DropColumn(
            name: "notional",
            table: "rfq_revisions");

        migrationBuilder.DropColumn(
            name: "sales_and_trading_message",
            table: "rfq_revisions");

        migrationBuilder.DropColumn(
            name: "quote_request_reason",
            table: "case_currents");

        migrationBuilder.DropColumn(
            name: "quote_status",
            table: "case_currents");

        migrationBuilder.AlterColumn<DateOnly>(
            name: "settlement_date",
            table: "rfq_revisions",
            type: "date",
            nullable: false,
            defaultValue: new DateOnly(1, 1, 1),
            oldClrType: typeof(DateOnly),
            oldType: "date",
            oldNullable: true);
    }
}
