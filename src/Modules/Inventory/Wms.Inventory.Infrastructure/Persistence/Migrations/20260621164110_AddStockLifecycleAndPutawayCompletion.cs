using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wms.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockLifecycleAndPutawayCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "allocated_to_wave_id",
                schema: "inventory",
                table: "stocks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "batch",
                schema: "inventory",
                table: "stocks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "expiry",
                schema: "inventory",
                table: "stocks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "location_id",
                schema: "inventory",
                table: "stocks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "picking_task_id",
                schema: "inventory",
                table: "stocks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "actual_destination_id",
                schema: "inventory",
                table: "putaway_tasks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "assigned_to",
                schema: "inventory",
                table: "putaway_tasks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_location_id",
                schema: "inventory",
                table: "putaway_tasks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "suggested_destination_id",
                schema: "inventory",
                table: "putaway_tasks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_stocks_allocated_to_wave_id",
                schema: "inventory",
                table: "stocks",
                column: "allocated_to_wave_id");

            migrationBuilder.CreateIndex(
                name: "ix_stocks_status_sku",
                schema: "inventory",
                table: "stocks",
                columns: new[] { "status", "sku" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_stocks_allocated_to_wave_id",
                schema: "inventory",
                table: "stocks");

            migrationBuilder.DropIndex(
                name: "ix_stocks_status_sku",
                schema: "inventory",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "allocated_to_wave_id",
                schema: "inventory",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "batch",
                schema: "inventory",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "expiry",
                schema: "inventory",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "location_id",
                schema: "inventory",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "picking_task_id",
                schema: "inventory",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "actual_destination_id",
                schema: "inventory",
                table: "putaway_tasks");

            migrationBuilder.DropColumn(
                name: "assigned_to",
                schema: "inventory",
                table: "putaway_tasks");

            migrationBuilder.DropColumn(
                name: "source_location_id",
                schema: "inventory",
                table: "putaway_tasks");

            migrationBuilder.DropColumn(
                name: "suggested_destination_id",
                schema: "inventory",
                table: "putaway_tasks");
        }
    }
}
