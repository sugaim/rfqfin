using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rfq.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHitAwayContactOwnerAndMemos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "closed_quote_id",
                table: "case_currents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sales_memos",
                columns: table => new
                {
                    case_id = table.Column<long>(type: "bigint", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_memos", x => x.case_id);
                    table.ForeignKey(
                        name: "FK_sales_memos_rfq_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "rfq_cases",
                        principalColumn: "case_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trader_memos",
                columns: table => new
                {
                    case_id = table.Column<long>(type: "bigint", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trader_memos", x => x.case_id);
                    table.ForeignKey(
                        name: "FK_trader_memos_rfq_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "rfq_cases",
                        principalColumn: "case_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO sales_memos (case_id, value, version)
                SELECT case_id, '', 1
                FROM rfq_cases
                ON CONFLICT (case_id) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO trader_memos (case_id, value, version)
                SELECT case_id, '', 1
                FROM rfq_cases
                ON CONFLICT (case_id) DO NOTHING;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_case_currents_closed_quote_id",
                table: "case_currents",
                column: "closed_quote_id");

            migrationBuilder.AddForeignKey(
                name: "FK_case_currents_confirmed_quotes_closed_quote_id",
                table: "case_currents",
                column: "closed_quote_id",
                principalTable: "confirmed_quotes",
                principalColumn: "quote_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_case_currents_confirmed_quotes_closed_quote_id",
                table: "case_currents");

            migrationBuilder.DropTable(name: "sales_memos");
            migrationBuilder.DropTable(name: "trader_memos");

            migrationBuilder.DropIndex(
                name: "IX_case_currents_closed_quote_id",
                table: "case_currents");

            migrationBuilder.DropColumn(
                name: "closed_quote_id",
                table: "case_currents");
        }
    }
}
