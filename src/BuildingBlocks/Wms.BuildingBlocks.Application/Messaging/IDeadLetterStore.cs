namespace Wms.BuildingBlocks.Application.Messaging;

// What: Port — Dead Letter Channel sink (EIP; ADR-0010 amendment)
// Why: pesan racun (gagal melewati batas retry) tak boleh hilang diam-diam — ia
// dipindahkan ke store forensik agar bisa diinspeksi/replay. Sebagai port, ia
// dikonsumsi SEMUA adapter messaging (Outbox dispatcher sekarang; consumer broker
// nanti) sehingga kebijakan DLQ seragam lintas-cloud. Implementasi konkret di
// Platform.<Cloud> (Local: tabel Postgres dead_letter).
// How: satu operasi async StoreAsync; pemanggil sudah memutuskan "ini poison",
// store hanya mempersist. Idempotency forensik ditoleransi (duplikat aman dibuang).
public interface IDeadLetterStore
{
    Task StoreAsync(DeadLetterMessage message, CancellationToken cancellationToken = default);
}
