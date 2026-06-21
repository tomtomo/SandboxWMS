using Npgsql;
using Testcontainers.PostgreSql;

namespace Wms.Inbound.IntegrationTests;

// What: shared Testcontainers Postgres fixture (reusable integration harness)
// Why: integration test rail butuh Postgres NYATA (bukan in-memory) supaya perilaku
// composite PK, tx, dan migration ter-uji sungguhan. Satu container per collection
// menekan biaya start; tiap test minta database segar untuk isolasi.
// How: IAsyncLifetime start/stop container; CreateDatabaseAsync bikin DB unik lalu
// kembalikan connection string-nya.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    // database baru per test → schema/state tak saling bocor antar test
    public async Task<string> CreateDatabaseAsync()
    {
        var databaseName = "t" + Guid.NewGuid().ToString("N");

        await using (var connection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = databaseName,
        }.ConnectionString;
    }
}

// What: xUnit collection — satu PostgresFixture dibagi semua test class di collection
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
