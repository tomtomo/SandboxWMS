using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Wms.Inbound.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRichGoodsReceiptAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gr_lines",
                schema: "inbound");

            migrationBuilder.AddColumn<string>(
                name: "dock_door",
                schema: "inbound",
                table: "goods_receipts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hold_reason",
                schema: "inbound",
                table: "goods_receipts",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "po_ref",
                schema: "inbound",
                table: "goods_receipts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplier_id",
                schema: "inbound",
                table: "goods_receipts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "gr_attachments",
                schema: "inbound",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    blob_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_by = table.Column<string>(type: "text", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gr_attachments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gr_discrepancies",
                schema: "inbound",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gr_discrepancies", x => x.id);
                    table.ForeignKey(
                        name: "fk_gr_discrepancies_goods_receipts_goods_receipt_id",
                        column: x => x.goods_receipt_id,
                        principalSchema: "inbound",
                        principalTable: "goods_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gr_expected_lines",
                schema: "inbound",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expected_qty = table.Column<int>(type: "integer", nullable: false),
                    uom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gr_expected_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_gr_expected_lines_goods_receipts_goods_receipt_id",
                        column: x => x.goods_receipt_id,
                        principalSchema: "inbound",
                        principalTable: "goods_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gr_scanned_lines",
                schema: "inbound",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    actual_qty = table.Column<int>(type: "integer", nullable: false),
                    batch = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    line_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gr_scanned_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_gr_scanned_lines_goods_receipts_goods_receipt_id",
                        column: x => x.goods_receipt_id,
                        principalSchema: "inbound",
                        principalTable: "goods_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gr_attachments_goods_receipt_id",
                schema: "inbound",
                table: "gr_attachments",
                column: "goods_receipt_id");

            migrationBuilder.CreateIndex(
                name: "ix_gr_discrepancies_goods_receipt_id",
                schema: "inbound",
                table: "gr_discrepancies",
                column: "goods_receipt_id");

            migrationBuilder.CreateIndex(
                name: "ix_gr_expected_lines_goods_receipt_id",
                schema: "inbound",
                table: "gr_expected_lines",
                column: "goods_receipt_id");

            migrationBuilder.CreateIndex(
                name: "ix_gr_scanned_lines_goods_receipt_id",
                schema: "inbound",
                table: "gr_scanned_lines",
                column: "goods_receipt_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gr_attachments",
                schema: "inbound");

            migrationBuilder.DropTable(
                name: "gr_discrepancies",
                schema: "inbound");

            migrationBuilder.DropTable(
                name: "gr_expected_lines",
                schema: "inbound");

            migrationBuilder.DropTable(
                name: "gr_scanned_lines",
                schema: "inbound");

            migrationBuilder.DropColumn(
                name: "dock_door",
                schema: "inbound",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "hold_reason",
                schema: "inbound",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "po_ref",
                schema: "inbound",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                schema: "inbound",
                table: "goods_receipts");

            migrationBuilder.CreateTable(
                name: "gr_lines",
                schema: "inbound",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gr_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_gr_lines_goods_receipts_goods_receipt_id",
                        column: x => x.goods_receipt_id,
                        principalSchema: "inbound",
                        principalTable: "goods_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gr_lines_goods_receipt_id",
                schema: "inbound",
                table: "gr_lines",
                column: "goods_receipt_id");
        }
    }
}
