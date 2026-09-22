using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rfq.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialDatabaseFoundation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "seed_markers",
            columns: table => new
            {
                key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_seed_markers", x => x.key);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "seed_markers");
    }
}
