using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rfq.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPostProcessBusinessDates : Migration
{
    private static readonly string[] EventBusinessDateIndexColumns =
        ["business_date", "type"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "business_date",
            table: "rfq_events",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "created_business_date",
            table: "rfq_cases",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "closed_business_date",
            table: "case_currents",
            type: "date",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_rfq_events_business_date_type",
            table: "rfq_events",
            columns: EventBusinessDateIndexColumns);

        migrationBuilder.CreateIndex(
            name: "ix_rfq_cases_created_business_date",
            table: "rfq_cases",
            column: "created_business_date");

        migrationBuilder.CreateIndex(
            name: "ix_case_currents_rfq_status",
            table: "case_currents",
            column: "rfq_status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_rfq_events_business_date_type",
            table: "rfq_events");

        migrationBuilder.DropIndex(
            name: "ix_rfq_cases_created_business_date",
            table: "rfq_cases");

        migrationBuilder.DropIndex(
            name: "ix_case_currents_rfq_status",
            table: "case_currents");

        migrationBuilder.DropColumn(
            name: "business_date",
            table: "rfq_events");

        migrationBuilder.DropColumn(
            name: "created_business_date",
            table: "rfq_cases");

        migrationBuilder.DropColumn(
            name: "closed_business_date",
            table: "case_currents");
    }
}
