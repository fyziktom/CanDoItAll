using CanDoItAll.Tests.Support;
using Npgsql;

namespace CanDoItAll.Tests.Unit;

public sealed class PostgresTestDatabaseCleanupTests {
    [Fact]
    public async Task Delayed_session_termination_can_finish_on_the_second_drop() {
        var calls = 0;
        await PostgresTestDatabaseLease.DropDatabaseAsync("isolated-cleanup", () => {
            calls++;
            return calls == 1 ? Task.FromException(Failure(PostgresErrorCodes.ObjectInUse)) : Task.CompletedTask;
        });
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(PostgresErrorCodes.ObjectInUse, 2)]
    [InlineData(PostgresErrorCodes.InsufficientPrivilege, 1)]
    [InlineData(PostgresErrorCodes.InvalidCatalogName, 1)]
    public async Task Persistent_or_unrelated_server_failures_are_preserved(string code, int expectedCalls) {
        var calls = 0;
        var failure = Failure(code);
        var observed = await Assert.ThrowsAsync<PostgresException>(() =>
            PostgresTestDatabaseLease.DropDatabaseAsync("isolated-cleanup", () => {
                calls++;
                return Task.FromException(failure);
            }));
        Assert.Same(failure, observed);
        Assert.Equal(expectedCalls, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transport_timeout_and_cancellation_are_not_retried(bool cancelled) {
        var calls = 0;
        Exception failure = cancelled ? new OperationCanceledException() : new NpgsqlException("Timed out", new TimeoutException());
        var observed = await Record.ExceptionAsync(() =>
            PostgresTestDatabaseLease.DropDatabaseAsync("isolated-cleanup", () => {
                calls++;
                return Task.FromException(failure);
            }));
        Assert.Same(failure, observed);
        Assert.Equal(1, calls);
    }

    private static PostgresException Failure(string code) => new("Fixture database cleanup failed", "ERROR", "ERROR", code);
}
