using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wms.Inbound.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "infrastructure");

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
                name: "dead_letter",
                schema: "infrastructure");

            migrationBuilder.DropTable(
                name: "inbox",
                schema: "infrastructure");

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "infrastructure");
        }
    }
}
