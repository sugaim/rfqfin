using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rfq.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RfqDbContext))]
[Migration("20260923120000_AddRevisionDraftCreatedBusinessDate")]
public sealed class AddRevisionDraftCreatedBusinessDate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "draft_created_business_date",
            table: "rfq_revisions",
            type: "date",
            nullable: true);

        // This repository's existing rows are deterministic development/demo data. Backfill
        // from the configured authoritative Business Date rather than inventing a date from UTC.
        migrationBuilder.Sql(
            """
            UPDATE rfq_revisions
            SET draft_created_business_date = (
                SELECT business_date
                FROM business_dates
                WHERE key = 'business-today')
            WHERE draft_created_business_date IS NULL;
            """);

        migrationBuilder.AlterColumn<DateOnly>(
            name: "draft_created_business_date",
            table: "rfq_revisions",
            type: "date",
            nullable: false,
            oldClrType: typeof(DateOnly),
            oldType: "date",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_rfq_revisions_draft_created_business_date",
            table: "rfq_revisions",
            column: "draft_created_business_date");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_rfq_revisions_draft_created_business_date",
            table: "rfq_revisions");
        migrationBuilder.DropColumn(
            name: "draft_created_business_date",
            table: "rfq_revisions");
    }
}
