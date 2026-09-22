using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rfq.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddMasterSearchAndDefaults : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "settlement_date",
            table: "rfq_revisions",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "standard_settlement_date",
            table: "rfq_revisions",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "category_snapshot",
            table: "rfq_cases",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "assigned_trader_id",
            table: "case_currents",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "contact_owner_id",
            table: "case_currents",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "owned",
            table: "case_currents",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql(
            """
            UPDATE rfq_cases
            SET category_snapshot = 'OTHER';

            UPDATE case_currents AS current
            SET contact_owner_id = rfq_case.created_by,
                assigned_trader_id = 'trader-a'
            FROM rfq_cases AS rfq_case
            WHERE current.case_id = rfq_case.case_id;

            UPDATE rfq_revisions AS revision
            SET settlement_date = (rfq_case.created_at AT TIME ZONE 'Asia/Tokyo')::date + 2,
                standard_settlement_date = (rfq_case.created_at AT TIME ZONE 'Asia/Tokyo')::date + 2
            FROM rfq_cases AS rfq_case
            WHERE revision.case_id = rfq_case.case_id;
            """);

        migrationBuilder.AlterColumn<DateOnly>(
            name: "settlement_date",
            table: "rfq_revisions",
            type: "date",
            nullable: false,
            oldClrType: typeof(DateOnly),
            oldType: "date",
            oldNullable: true);

        migrationBuilder.AlterColumn<DateOnly>(
            name: "standard_settlement_date",
            table: "rfq_revisions",
            type: "date",
            nullable: false,
            oldClrType: typeof(DateOnly),
            oldType: "date",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "category_snapshot",
            table: "rfq_cases",
            type: "character varying(50)",
            maxLength: 50,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "assigned_trader_id",
            table: "case_currents",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "contact_owner_id",
            table: "case_currents",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true);

        migrationBuilder.CreateTable(
            name: "categories",
            columns: table => new
            {
                category_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_categories", x => x.category_id);
            });

        migrationBuilder.CreateTable(
            name: "clients",
            columns: table => new
            {
                client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_clients", x => x.client_id);
            });

        migrationBuilder.CreateTable(
            name: "desks",
            columns: table => new
            {
                desk_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_desks", x => x.desk_id);
            });

        migrationBuilder.CreateTable(
            name: "securities",
            columns: table => new
            {
                security_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                japanese_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                bbg_display = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                bbg_search_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                internal_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                isin = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                category_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_securities", x => x.security_id);
                table.ForeignKey(
                    name: "FK_securities_categories_category_id",
                    column: x => x.category_id,
                    principalTable: "categories",
                    principalColumn: "category_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "master_users",
            columns: table => new
            {
                user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                desk_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                roles = table.Column<string[]>(type: "text[]", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_master_users", x => x.user_id);
                table.ForeignKey(
                    name: "FK_master_users_desks_desk_id",
                    column: x => x.desk_id,
                    principalTable: "desks",
                    principalColumn: "desk_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "category_routings",
            columns: table => new
            {
                category_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                default_trader_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_category_routings", x => x.category_id);
                table.ForeignKey(
                    name: "FK_category_routings_categories_category_id",
                    column: x => x.category_id,
                    principalTable: "categories",
                    principalColumn: "category_id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_category_routings_master_users_default_trader_id",
                    column: x => x.default_trader_id,
                    principalTable: "master_users",
                    principalColumn: "user_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_category_routings_default_trader_id",
            table: "category_routings",
            column: "default_trader_id");

        migrationBuilder.CreateIndex(
            name: "ux_clients_code",
            table: "clients",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_master_users_desk_id",
            table: "master_users",
            column: "desk_id");

        migrationBuilder.CreateIndex(
            name: "IX_securities_category_id",
            table: "securities",
            column: "category_id");

        migrationBuilder.CreateIndex(
            name: "ux_securities_internal_code",
            table: "securities",
            column: "internal_code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_securities_isin",
            table: "securities",
            column: "isin",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "category_routings");

        migrationBuilder.DropTable(
            name: "clients");

        migrationBuilder.DropTable(
            name: "securities");

        migrationBuilder.DropTable(
            name: "master_users");

        migrationBuilder.DropTable(
            name: "categories");

        migrationBuilder.DropTable(
            name: "desks");

        migrationBuilder.DropColumn(
            name: "settlement_date",
            table: "rfq_revisions");

        migrationBuilder.DropColumn(
            name: "standard_settlement_date",
            table: "rfq_revisions");

        migrationBuilder.DropColumn(
            name: "category_snapshot",
            table: "rfq_cases");

        migrationBuilder.DropColumn(
            name: "assigned_trader_id",
            table: "case_currents");

        migrationBuilder.DropColumn(
            name: "contact_owner_id",
            table: "case_currents");

        migrationBuilder.DropColumn(
            name: "owned",
            table: "case_currents");
    }
}
