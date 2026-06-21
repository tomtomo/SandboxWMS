using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Wms.TestSupport;

// What: shared Testcontainers Postgres fixture (reusable integration harness)
// Why: integration test butuh Postgres NYATA (bukan in-memory) supaya composite PK, tx,
// dan migration ter-uji sungguhan. Di-extract ke project bersama begitu modul ke-2
// (Inventory) + E2E ikut memakainya — hindari duplikasi container logic. Satu container
// per collection menekan biaya start; tiap test minta database segar untuk isolasi.
// How: IAsyncLifetime start/stop container; CreateDatabaseAsync bikin DB unik lalu
// kembalikan connection string-nya. (CollectionDefinition tetap per-assembly — xUnit
// mengikat collection ke assembly-nya.)
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
