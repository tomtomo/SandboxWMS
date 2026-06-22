using Wms.TestSupport;

namespace Wms.Notification.IntegrationTests;

// What: xUnit collection fixture — satu Postgres container (Testcontainers) di-share lintas test class
// Why: container start mahal → share via ICollectionFixture; tiap test buat DATABASE baru (isolasi state).
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
