using Wms.BuildingBlocks.Application.Abstractions;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Application.Abstractions;

// What: Repository Pattern (DDD) — port Stock; Add/GetById dari IRepository + query domain + Remove
// Why: consumer menulis & meng-query Stock via abstraksi (DIP, FF#5); commit dipisah ke IUnitOfWork. Query
// mengembalikan aggregate TRACKED → transisi (Allocate/Pick/Putaway) ter-persist. Remove = hard-delete saat
// barang keluar gudang (dispatch) — tetap di port ini (bukan base; hanya Stock yang hard-delete).
public interface IStockRepository : IRepository<Stock, StockId>
{
    // What: kandidat alokasi — semua Stock Available untuk sku yang diminta (FEFO dipilih di consumer)
    // Why: dimuat sekali (tracked) lalu di-FEFO-pick in-memory agar pilihan antar-line dalam satu wave
    // tak double-allocate stock yang sama (perubahan in-memory belum terlihat query DB berikutnya).
    Task<IReadOnlyList<Stock>> ListAvailableBySkusAsync(
        IReadOnlyCollection<string> skus, CancellationToken cancellationToken = default);

    // What: semua Stock state Picked terikat ke wave (ShipmentDispatched → removed)
    Task<IReadOnlyList<Stock>> ListPickedByWaveAsync(Guid waveId, CancellationToken cancellationToken = default);

    // What: hapus Stock (barang keluar gudang saat dispatch); commit oleh IUnitOfWork
    void Remove(Stock stock);
}
