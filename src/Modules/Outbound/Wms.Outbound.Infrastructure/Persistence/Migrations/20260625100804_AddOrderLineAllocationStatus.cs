using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wms.Outbound.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLineAllocationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CATATAN: generator EF keliru meng-emit AddColumn `xmin` (system column PostgreSQL) untuk
            // outbound_orders/waves/picking_tasks karena snapshot lama (pra-ADR-0031) belum merekamnya —
            // di-HAPUS manual (xmin auto-ada di PG, AddColumn akan gagal). Snapshot tetap merekam xmin →
            // drift teratasi (migrasi Outbound berikutnya tak re-emit). Lihat OutboundDbContext.cs.
            migrationBuilder.AddColumn<string>(
                name: "allocation_status",
                schema: "outbound",
                table: "order_lines",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Pending");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allocation_status",
                schema: "outbound",
                table: "order_lines");
        }
    }
}
