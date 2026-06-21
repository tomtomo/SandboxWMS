using Wms.TestSupport;

namespace Wms.Inbound.IntegrationTests;

// What: xUnit collection — satu PostgresFixture (shared, Wms.TestSupport) per assembly
// Why: collection definition mengikat fixture ke assembly ini (xUnit per-assembly);
// fixture class-nya sendiri dibagi lewat Wms.TestSupport (DRY).
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
