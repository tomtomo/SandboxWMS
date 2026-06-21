using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wms.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogAndAuditableFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                schema: "inventory",
                table: "stocks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                schema: "inventory",
                table: "stocks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "modified_at",
                schema: "inventory",
                table: "stocks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                schema: "inventory",
                table: "stocks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                schema: "inventory",
                table: "putaway_tasks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                schema: "inventory",
                table: "putaway_tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "modified_at",
                schema: "inventory",
                table: "putaway_tasks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                schema: "inventory",
                table: "putaway_tasks",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_log",
                schema: "infrastructure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    aggregate_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_success = table.Column<bool>(type: "boolean", nullable: false),
                    error_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    payload = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    traceparent = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_aggregate_type_aggregate_id_occurred_at",
                schema: "infrastructure",
                table: "audit_log",
                columns: new[] { "aggregate_type", "aggregate_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log",
                schema: "infrastructure");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "inventory",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "inventory",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "modified_at",
                schema: "inventory",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "modified_by",
                schema: "inventory",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "inventory",
                table: "putaway_tasks");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "inventory",
                table: "putaway_tasks");

            migrationBuilder.DropColumn(
                name: "modified_at",
                schema: "inventory",
                table: "putaway_tasks");

            migrationBuilder.DropColumn(
                name: "modified_by",
                schema: "inventory",
                table: "putaway_tasks");
        }
    }
}
