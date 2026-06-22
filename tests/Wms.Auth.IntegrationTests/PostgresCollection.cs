using Wms.TestSupport;

namespace Wms.Auth.IntegrationTests;

// What: xUnit collection — satu PostgresFixture (shared, Wms.TestSupport) per assembly
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
