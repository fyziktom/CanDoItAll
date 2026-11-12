using CanDoItAll.Tests.Support;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "HostPlatform")]
public sealed class PostgresTestDatabaseLeaseTests {
    [Fact]
    public async Task Disposal_terminates_its_held_connection_and_preserves_another_database_lease() {
        await using var survivor = PostgresTestDatabaseLease.Create("cleanup-survivor");
        await using var removed = PostgresTestDatabaseLease.Create("cleanup-target");
        await using var held = new NpgsqlConnection(removed.ConnectionString);
        await held.OpenAsync();
        await using var independent = new NpgsqlConnection(survivor.ConnectionString);
        await independent.OpenAsync();

        await removed.DisposeAsync();

        await using var inspect = independent.CreateCommand();
        inspect.CommandText = "select exists(select 1 from pg_database where datname = @database);";
        inspect.Parameters.AddWithValue("database", removed.DatabaseName);
        Assert.False(Assert.IsType<bool>(await inspect.ExecuteScalarAsync()));
        inspect.Parameters[0].Value = survivor.DatabaseName;
        Assert.True(Assert.IsType<bool>(await inspect.ExecuteScalarAsync()));
        await using var stopped = held.CreateCommand();
        stopped.CommandText = "select 1;";
        await Assert.ThrowsAnyAsync<NpgsqlException>(() => stopped.ExecuteScalarAsync());
    }
}
