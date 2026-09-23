using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

[CollectionDefinition(nameof(PostgresAvailabilityEnvironment), DisableParallelization = true)]
public sealed class PostgresAvailabilityEnvironment;

[Collection(nameof(PostgresAvailabilityEnvironment))]
public sealed class PostgresTestAvailabilityTests {
    [Fact]
    public async Task Missing_explicit_test_server_fails_without_probing_development_or_starting_compose() {
        const string variable = "CANDOITALL_TESTS_POSTGRES_CONNECTION";
        var previous = Environment.GetEnvironmentVariable(variable);
        try {
            Environment.SetEnvironmentVariable(variable, null);
            var result = await PostgresTestAvailability.EnsureAvailableAsync(Path.GetTempPath());
            Assert.False(result.IsAvailable);
            Assert.False(result.ProvisionedByDocker);
            Assert.Null(result.ConnectionString);
            Assert.Contains(variable, result.Message, StringComparison.Ordinal);
            Assert.Contains("isolated PostgreSQL 18", result.Message, StringComparison.Ordinal);
        } finally {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }
}
