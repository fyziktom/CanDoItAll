using System.Data;
using System.Data.Common;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionDispatchAuthorityFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessOriginalAuthorityConnectionTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, ReadKind.OriginalReceipt)]
    [InlineData(ReadBasisMode.StepSnapshot, ReadKind.OriginalReceipt)]
    [InlineData(ReadBasisMode.RunSnapshot, ReadKind.SelectedDispatch)]
    [InlineData(ReadBasisMode.StepSnapshot, ReadKind.SelectedDispatch)]
    public async Task Each_original_observation_uses_one_fresh_connection_for_all_four_reads(ReadBasisMode mode, ReadKind kind) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var fixture = await CreateAsync(context, scope.ServiceProvider, mode, prepared: true);
        var probe = new ConnectionProbe();
        var query = Query(scope.ServiceProvider, clock, probe);
        for (var attempt = 0; attempt < 2; attempt++) {
            var result = await ReadAsync(query, fixture, kind);
            Assert.Equal(ProcessExecutionAuthorityDisposition.Bound, result.Disposition);
            Assert.Equal(kind == ReadKind.SelectedDispatch, result.Snapshot!.ObservedCurrentDispatch);
            Assert.Equal(4 * (attempt + 1), probe.Reads);
            AssertClosed(probe, attempt + 1);
        }
        Assert.NotSame(probe.Connections[0], probe.Connections[1]);
    }

    [Theory]
    [InlineData(ReadExit.MissingClaim, ReadKind.OriginalReceipt)]
    [InlineData(ReadExit.ChangedMaterial, ReadKind.OriginalReceipt)]
    [InlineData(ReadExit.CancelledBeforeRead, ReadKind.OriginalReceipt)]
    [InlineData(ReadExit.CancelledDuringRead, ReadKind.OriginalReceipt)]
    [InlineData(ReadExit.QueryFailure, ReadKind.OriginalReceipt)]
    [InlineData(ReadExit.MissingClaim, ReadKind.SelectedDispatch)]
    [InlineData(ReadExit.ChangedMaterial, ReadKind.SelectedDispatch)]
    [InlineData(ReadExit.CancelledBeforeRead, ReadKind.SelectedDispatch)]
    [InlineData(ReadExit.CancelledDuringRead, ReadKind.SelectedDispatch)]
    [InlineData(ReadExit.QueryFailure, ReadKind.SelectedDispatch)]
    public async Task Refusal_cancellation_and_query_failure_release_the_connection(ReadExit exit, ReadKind kind) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var fixture = await CreateAsync(context, scope.ServiceProvider, prepared: true);
        using var cancellation = new CancellationTokenSource();
        var probe = new ConnectionProbe {
            BeforeRead = (ordinal, token) => {
                if (ordinal == 2 && exit == ReadExit.CancelledDuringRead) {
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                }
                if (ordinal == 2 && exit == ReadExit.QueryFailure) {
                    throw new IOException("original-source-read-failed");
                }
                return Task.CompletedTask;
            }
        };
        if (exit == ReadExit.MissingClaim) {
            fixture = fixture with { Evidence = fixture.Evidence with { DispatchClaimToken = Guid.NewGuid() } };
        }
        if (exit == ReadExit.ChangedMaterial) {
            fixture = fixture with { Original = fixture.Original with {
                Binding = fixture.Original.Binding with { Material = fixture.Original.Binding.Material with {
                    Fingerprint = DifferentFingerprint(fixture.Original.Material.Fingerprint)
                } }
            } };
        }
        if (exit == ReadExit.CancelledBeforeRead) {
            cancellation.Cancel();
        }
        Task<ProcessExecutionDispatchAuthorityResult> ObserveAsync()
            => ReadAsync(Query(scope.ServiceProvider, clock, probe), fixture, kind, cancellation.Token);
        switch (exit) {
            case ReadExit.MissingClaim:
                var result = await ObserveAsync();
                Assert.Equal(ProcessExecutionAuthorityDisposition.DispatchBindingChanged, result.Disposition);
                Assert.Null(result.Snapshot);
                break;
            case ReadExit.ChangedMaterial:
                await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(ObserveAsync);
                break;
            case ReadExit.CancelledBeforeRead or ReadExit.CancelledDuringRead:
                await Assert.ThrowsAnyAsync<OperationCanceledException>(ObserveAsync);
                break;
            case ReadExit.QueryFailure:
                Assert.Equal("original-source-read-failed", (await Assert.ThrowsAsync<IOException>(ObserveAsync)).Message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(exit));
        }
        Assert.Equal(exit switch {
            ReadExit.CancelledBeforeRead => 0,
            ReadExit.MissingClaim => 1,
            ReadExit.ChangedMaterial => 4,
            _ => 2
        }, probe.Reads);
        AssertClosed(probe, exit == ReadExit.CancelledBeforeRead ? 0 : 1);
    }

    [Theory]
    [InlineData(ReadKind.OriginalReceipt)]
    [InlineData(ReadKind.SelectedDispatch)]
    public async Task A_connection_does_not_hide_original_material_changed_between_queries(ReadKind kind) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var fixture = await CreateAsync(context, scope.ServiceProvider, prepared: true);
        var changed = false;
        var probe = new ConnectionProbe {
            BeforeRead = async (ordinal, token) => {
                if (ordinal != 4) {
                    return;
                }
                await using var writer = Context(scope.ServiceProvider);
                Assert.Equal(1, await writer.DecisionMaterials
                    .Where(item => item.MaterialId == fixture.Original.Material.Reference.MaterialId.Value)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.Fingerprint,
                        DifferentFingerprint(fixture.Original.Material.Fingerprint)), token));
                changed = true;
            }
        };
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => ReadAsync(Query(scope.ServiceProvider, clock, probe), fixture, kind));
        Assert.True(changed);
        Assert.Equal(4, probe.Reads);
        AssertClosed(probe, 1);
    }

    [Theory]
    [InlineData(ReadKind.OriginalReceipt)]
    [InlineData(ReadKind.SelectedDispatch)]
    public async Task Test_only_nonrelational_reads_preserve_missing_source_semantics(ReadKind kind) {
        ProcessRunId run = new(Guid.NewGuid());
        ProcessStepInstanceId step = new(Guid.NewGuid());
        ProcessDecisionChildAddress address = new(run, run, step, new(new(Guid.NewGuid()), 1), new(Guid.NewGuid()));
        ProcessDecisionMaterialScope scope = new(new(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), run, run, step);
        ProcessDecisionMaterialReference material = new(scope.Id, 1, new(Guid.NewGuid()));
        ProcessDecisionChildMaterialBinding original = new(
            new(address, material.MaterialId, ProcessDecisionChildOwner.Agent, new(Guid.NewGuid()), new(Guid.NewGuid()), new(10, 0)),
            scope, new(material, new string('A', 64)), ProcessDecisionPolicyFixture.Contract(ProcessDecisionChildOwner.Agent));
        ProcessExecutionClaimEvidence evidence = new(address.ChildId.Value, Guid.NewGuid(), run, step, Guid.NewGuid(),
            "connection-test", ProcessProjectAdmissionFixture.Now, true);
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        var query = new EfProcessExecutionAuthorityQuery(new ContextFactory(options), TimeProvider.System);
        var result = await (kind switch {
            ReadKind.OriginalReceipt => query.ReadOriginalDecisionAsync(evidence, original, original.ExecutionContract, null),
            ReadKind.SelectedDispatch => query.ReadDecisionDispatchAsync(evidence, original, original.ExecutionContract, null),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        });
        Assert.Equal(ProcessExecutionAuthorityDisposition.DispatchBindingChanged, result.Disposition);
        Assert.Null(result.Snapshot);
    }

    private static Task<ProcessExecutionDispatchAuthorityResult> ReadAsync(EfProcessExecutionAuthorityQuery query,
        ProcessDecisionDispatchAuthorityFixture fixture, ReadKind kind, CancellationToken cancellationToken = default)
        => kind switch {
            ReadKind.OriginalReceipt => query.ReadOriginalDecisionAsync(fixture.Evidence, fixture.Original.Binding,
                fixture.Original.Binding.ExecutionContract, fixture.Original.Material.ContentContract, cancellationToken),
            ReadKind.SelectedDispatch => query.ReadDecisionDispatchAsync(fixture.Evidence, fixture.Original.Binding,
                fixture.Original.Binding.ExecutionContract, fixture.Original.Material.ContentContract, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    private static string DifferentFingerprint(string original) => new(original[0] == 'A' ? 'B' : 'A', 64);

    private static void AssertClosed(ConnectionProbe probe, int expected) {
        Assert.Equal(expected, probe.Connections.Count);
        Assert.Equal(expected, probe.Closed);
        Assert.Equal(0, probe.Writes);
        Assert.All(probe.Connections, connection => Assert.Equal(ConnectionState.Closed, connection.State));
    }

    public enum ReadExit { MissingClaim, ChangedMaterial, CancelledBeforeRead, CancelledDuringRead, QueryFailure }
    public enum ReadKind { OriginalReceipt, SelectedDispatch }

    private sealed class ContextFactory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }

    private sealed class ConnectionProbe : DbCommandInterceptor, IDbConnectionInterceptor {
        internal List<DbConnection> Connections { get; } = [];
        internal int Reads { get; private set; }
        internal int Closed { get; private set; }
        internal int Writes { get; private set; }
        internal Func<int, CancellationToken, Task>? BeforeRead { get; init; }

        public Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default) {
            Connections.Add(connection);
            return Task.CompletedTask;
        }

        public Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData) {
            Closed++;
            return Task.CompletedTask;
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Assert.Null(command.Transaction);
            Reads++;
            if (BeforeRead is { } inspect) {
                await inspect(Reads, cancellationToken);
            }
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Writes++;
            return ValueTask.FromResult(result);
        }
    }
}
