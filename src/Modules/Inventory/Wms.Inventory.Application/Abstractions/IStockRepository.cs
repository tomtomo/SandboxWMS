using Wms.Inventory.Domain;

namespace Wms.Inventory.Application.Abstractions;

// What: Repository Pattern (DDD) — port Stock (impl EF di Infrastructure)
// Why: consumer menulis & meng-query Stock via abstraksi ini, tak tahu EF (Dependency Inversion,
// FF#5); commit dipisah ke IUnitOfWork (satu transaksi dengan Inbox mark). Query mengembalikan
// aggregate TRACKED → mutasi transisi (Allocate/Pick) ter-persist saat SaveChanges.
public interface IStockRepository
{
    Task AddAsync(Stock stock, CancellationToken cancellationToken = default);

    // What: kandidat alokasi — semua Stock Available untuk sku yang diminta (FEFO dipilih di consumer)
    // Why: dimuat sekali (tracked) lalu di-FEFO-pick in-memory agar pilihan antar-line dalam satu wave
    // tak double-allocate stock yang sama (perubahan in-memory belum terlihat query DB berikutnya).
    Task<IReadOnlyList<Stock>> ListAvailableBySkusAsync(
        IReadOnlyCollection<string> skus, CancellationToken cancellationToken = default);

    // What: ambil satu Stock by id (PickingCompleted membawa stockId)
    Task<Stock?> GetAsync(StockId id, CancellationToken cancellationToken = default);

    // What: semua Stock state Picked terikat ke wave (ShipmentDispatched → removed)
    Task<IReadOnlyList<Stock>> ListPickedByWaveAsync(Guid waveId, CancellationToken cancellationToken = default);

    // What: hapus Stock (barang keluar gudang saat dispatch); commit oleh IUnitOfWork
    void Remove(Stock stock);
}
