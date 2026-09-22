using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rfq.Infrastructure.Migrations;

/// <inheritdoc />
public partial class RemoveQuoteEventCaseId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_quote_events_rfq_cases_case_id",
            table: "quote_events");

        migrationBuilder.DropIndex(
            name: "IX_quote_events_case_id",
            table: "quote_events");

        migrationBuilder.DropColumn(
            name: "case_id",
            table: "quote_events");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "case_id",
            table: "quote_events",
            type: "bigint",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE quote_events qe
            SET case_id = r.case_id
            FROM confirmed_quotes q
            JOIN rfq_revisions r ON r.revision_id = q.revision_id
            WHERE q.quote_id = qe.quote_id;
            """);

        migrationBuilder.AlterColumn<long>(
            name: "case_id",
            table: "quote_events",
            type: "bigint",
            nullable: false,
            oldClrType: typeof(long),
            oldType: "bigint",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_quote_events_case_id",
            table: "quote_events",
            column: "case_id");

        migrationBuilder.AddForeignKey(
            name: "FK_quote_events_rfq_cases_case_id",
            table: "quote_events",
            column: "case_id",
            principalTable: "rfq_cases",
            principalColumn: "case_id",
            onDelete: ReferentialAction.Cascade);
    }
}
