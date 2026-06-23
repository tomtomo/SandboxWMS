using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wms.MasterData.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApiIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "masterdata",
                table: "warehouses",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "masterdata",
                table: "products",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "masterdata",
                table: "locations",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "api_idempotency",
                schema: "infrastructure",
                columns: table => new
                {
                    endpoint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    response_body = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    traceparent = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_api_idempotency", x => new { x.endpoint, x.idempotency_key });
                });

            migrationBuilder.CreateIndex(
                name: "ix_api_idempotency_recorded_at",
                schema: "infrastructure",
                table: "api_idempotency",
                column: "recorded_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_idempotency",
                schema: "infrastructure");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "masterdata",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "masterdata",
                table: "products");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "masterdata",
                table: "locations");
        }
    }
}
