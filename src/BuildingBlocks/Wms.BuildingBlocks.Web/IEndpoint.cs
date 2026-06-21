using Microsoft.AspNetCore.Routing;

namespace Wms.BuildingBlocks.Web;

// What: Endpoint abstraction (Vertical Slice + REST untuk UI, ADR-0006)
// Why: tiap slice mendaftarkan endpoint REST-nya sendiri (self-contained), bukan
// controller terpusat — host cukup memanggil MapEndpoint untuk semua slice.
// How: implementasi konkret ada di tiap <Module>.Api; host menemukan & memanggilnya.
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
