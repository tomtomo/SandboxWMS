using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wms.Reporting.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "infrastructure");

            migrationBuilder.EnsureSchema(
                name: "reporting");

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

            migrationBuilder.CreateTable(
                name: "dead_letter",
                schema: "infrastructure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    logical_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    error = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    dead_lettered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    traceparent = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    tracestate = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dead_letter", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_summary",
                schema: "reporting",
                columns: table => new
                {
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    wave_count = table.Column<int>(type: "integer", nullable: false),
                    total_volume = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispatch_summary", x => x.day);
                });

            migrationBuilder.CreateTable(
                name: "inbox",
                schema: "infrastructure",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    handler_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox", x => new { x.event_id, x.handler_type });
                });

            migrationBuilder.CreateTable(
                name: "operator_activity",
                schema: "reporting",
                columns: table => new
                {
                    operator_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    putaway_count = table.Column<int>(type: "integer", nullable: false),
                    pick_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operator_activity", x => new { x.operator_id, x.day });
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                schema: "infrastructure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    logical_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    traceparent = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    tracestate = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "receiving_summary",
                schema: "reporting",
                columns: table => new
                {
                    supplier_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    gr_count = table.Column<int>(type: "integer", nullable: false),
                    received_qty = table.Column<int>(type: "integer", nullable: false),
                    rejected_qty = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receiving_summary", x => new { x.supplier_id, x.day });
                });

            migrationBuilder.CreateTable(
                name: "stock_on_hand_view",
                schema: "reporting",
                columns: table => new
                {
                    warehouse_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    batch = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    qty_on_hand = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_on_hand_view", x => new { x.warehouse_id, x.sku, x.batch });
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_aggregate_type_aggregate_id_occurred_at",
                schema: "infrastructure",
                table: "audit_log",
                columns: new[] { "aggregate_type", "aggregate_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_event_id",
                schema: "infrastructure",
                table: "dead_letter",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_processed_at_occurred_at",
                schema: "infrastructure",
                table: "outbox",
                columns: new[] { "processed_at", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log",
                schema: "infrastructure");

            migrationBuilder.DropTable(
                name: "dead_letter",
                schema: "infrastructure");

            migrationBuilder.DropTable(
                name: "dispatch_summary",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "inbox",
                schema: "infrastructure");

            migrationBuilder.DropTable(
                name: "operator_activity",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "infrastructure");

            migrationBuilder.DropTable(
                name: "receiving_summary",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "stock_on_hand_view",
                schema: "reporting");
        }
    }
}
