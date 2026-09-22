using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rfq.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddRfqDrafts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "rfq_cases",
            columns: table => new
            {
                case_id = table.Column<Guid>(type: "uuid", nullable: false),
                client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                security_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                sales_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_rfq_cases", x => x.case_id);
            });

        migrationBuilder.CreateTable(
            name: "rfq_revisions",
            columns: table => new
            {
                revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                case_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_rfq_revisions", x => x.revision_id);
                table.ForeignKey(
                    name: "FK_rfq_revisions_rfq_cases_case_id",
                    column: x => x.case_id,
                    principalTable: "rfq_cases",
                    principalColumn: "case_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "case_currents",
            columns: table => new
            {
                case_id = table.Column<Guid>(type: "uuid", nullable: false),
                lifecycle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                rfq_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                current_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_case_currents", x => x.case_id);
                table.ForeignKey(
                    name: "FK_case_currents_rfq_cases_case_id",
                    column: x => x.case_id,
                    principalTable: "rfq_cases",
                    principalColumn: "case_id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_case_currents_rfq_revisions_current_revision_id",
                    column: x => x.current_revision_id,
                    principalTable: "rfq_revisions",
                    principalColumn: "revision_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_case_currents_current_revision_id",
            table: "case_currents",
            column: "current_revision_id");

        migrationBuilder.CreateIndex(
            name: "ix_rfq_cases_sales_id_created_at",
            table: "rfq_cases",
            columns: ["sales_id", "created_at"]);

        migrationBuilder.CreateIndex(
            name: "ux_rfq_revisions_one_draft_per_case",
            table: "rfq_revisions",
            column: "case_id",
            unique: true,
            filter: "status = 'Draft'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "case_currents");

        migrationBuilder.DropTable(
            name: "rfq_revisions");

        migrationBuilder.DropTable(
            name: "rfq_cases");
    }
}
