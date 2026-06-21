namespace Wms.BuildingBlocks.Application.Auditing;

// What: Port — operational audit sink (ADR-0022; mirror IDeadLetterStore)
// Why: audit-log ditulis OUT-OF-BAND (koneksi/transaksi sendiri) agar SURVIVE rollback bisnis —
// forensik attempt-yang-gagal hanya mungkin bila tulisannya independen dari hasil transaksi
// bisnis. Sebagai port, kebijakan durability/store dipilih adapter Platform.<Cloud> (Local:
// tabel infrastructure.audit_log); EKSPLISIT bukan via Outbox — Outbox commit satu-tx dgn state,
// jadi ikut ter-rollback dan menggagalkan tujuan forensik.
// How: satu operasi async WriteAsync; AuditLogBehavior memanggilnya dari SCOPE BARU (DbContext
// segar) setelah TransactionBehavior menutup transaksi bisnis di dalamnya.
public interface IAuditLogStore
{
    Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}
