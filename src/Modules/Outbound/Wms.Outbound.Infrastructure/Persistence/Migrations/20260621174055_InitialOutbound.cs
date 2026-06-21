using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Wms.Outbound.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialOutbound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "infrastructure");

            migrationBuilder.EnsureSchema(
                name: "outbound");

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
                name: "outbound_orders",
                schema: "outbound",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ship_to = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    wave_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_by = table.Column<string>(type: "text", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbound_orders", x => x.id);
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
                name: "picking_tasks",
                schema: "outbound",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wave_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_location_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    batch = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    qty = table.Column<int>(type: "integer", nullable: false),
                    assigned_to = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    actual_qty = table.Column<int>(type: "integer", nullable: true),
                    staging_location_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_by = table.Column<string>(type: "text", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_picking_tasks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "waves",
                schema: "outbound",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    order_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    picking_task_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_by = table.Column<string>(type: "text", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_waves", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "order_lines",
                schema: "outbound",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    qty = table.Column<int>(type: "integer", nullable: false),
                    uom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    outbound_order_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_lines_outbound_orders_outbound_order_id",
                        column: x => x.outbound_order_id,
                        principalSchema: "outbound",
                        principalTable: "outbound_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "ix_order_lines_outbound_order_id",
                schema: "outbound",
                table: "order_lines",
                column: "outbound_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbound_orders_wave_id",
                schema: "outbound",
                table: "outbound_orders",
                column: "wave_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_processed_at_occurred_at",
                schema: "infrastructure",
                table: "outbox",
                columns: new[] { "processed_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_picking_tasks_wave_id",
                schema: "outbound",
                table: "picking_tasks",
                column: "wave_id");
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
                name: "inbox",
                schema: "infrastructure");

            migrationBuilder.DropTable(
                name: "order_lines",
                schema: "outbound");

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "infrastructure");

            migrationBuilder.DropTable(
                name: "picking_tasks",
                schema: "outbound");

            migrationBuilder.DropTable(
                name: "waves",
                schema: "outbound");

            migrationBuilder.DropTable(
                name: "outbound_orders",
                schema: "outbound");
        }
    }
}
