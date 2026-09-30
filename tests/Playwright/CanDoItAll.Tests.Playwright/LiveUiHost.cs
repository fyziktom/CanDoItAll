using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

using static CanDoItAll.Tests.Playwright.Smoke.AgentUiJourneySupport;

namespace CanDoItAll.Tests.Playwright.Smoke;

internal enum LiveUiStopReason { None, ToolBatchLimit, RequestBudgetRefusal, Timeout, ExplicitCancellation }

internal sealed class LiveUiHost : IAsyncDisposable {
    private readonly PlaywrightAppFixture fixture = new();
    private readonly CancellationTokenSource stopWatchdog = new();
    private readonly string artifacts;
    private ServiceProvider? owner;
    private IBrowserContext? context;
    private Task watchdog = Task.CompletedTask;
    private Guid watchedAgentId;
    private LiveModelRequestProxy? requestProxy;
    private Guid? boundedProviderId;
    private Task? disposal;
    internal LiveUiStopReason StopReason { get; private set; }
    internal int ToolAdmissionBatches { get; private set; }
    internal int WatchdogReadFailures { get; private set; }
    internal string? WatchdogReadFailureType { get; private set; }
    private const int MaximumToolBatchesPerExecution = 10;

    private LiveUiHost() {
        artifacts = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "crm-hr-live-agent-tools", $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifacts);
    }

    internal string BaseUrl => fixture.BaseUrl;

    internal bool SpendBoundExceeded { get; private set; }
    internal int ProviderJournalRequests { get; private set; }

    internal static async Task<LiveUiHost> StartAsync() {
        var host = new LiveUiHost();
        try {
            await host.fixture.InitializeAsync();
            host.owner = await TestApplicationBootstrap.BuildServiceProviderAsync(
                host.fixture.OwnedDatabaseProfile,
                "CanDoItAll.Tests.Playwright.LiveAgentSeed",
                TestSchemaBootstrapModules.Full,
                new Dictionary<string, string?> { ["DevelopmentManager:TuningModeEnabled"] = "false" });
            return host;
        } catch {
            await host.DisposeAsync();
            throw;
        }
    }

    internal string ArtifactDirectory => artifacts;
    internal string Artifact(string fileName) => Path.Combine(artifacts, fileName);

    internal async Task<T> SeedAsync<T>(Func<IServiceProvider, Task<T>> read) {
        await using var scope = owner!.CreateAsyncScope();
        return await read(scope.ServiceProvider);
    }

    internal async Task<IPage> NewPageAsync() {
        context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(45_000);
        return page;
    }

    internal Task<int> CountModelRequestsAsync() => Task.FromResult(requestProxy?.Admitted ?? 0);

    internal async Task BoundProviderAsync(Guid providerId) {
        if (IsRehearsal() || boundedProviderId == providerId) {
            return;
        }
        if (boundedProviderId.HasValue) {
            throw new InvalidOperationException("A live execution cannot switch its admitted provider.");
        }
        requestProxy = await LiveModelRequestProxy.StartAsync();
        await SeedAsync(async services => {
            var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
            var editor = await workspace.GetProviderEditorAsync(providerId);
            Assert.Equal(ProviderKind.OpenAi, editor.Kind);
            Assert.Equal(ProviderTransportKind.Responses, editor.Transport);
            Assert.Equal("https://api.openai.com/v1", editor.BaseUrl.TrimEnd('/'));
            editor.BaseUrl = requestProxy.BaseUrl;
            Assert.Equal(providerId, await workspace.SaveProviderAsync(editor));
            return true;
        });
        boundedProviderId = providerId;
    }

    internal Task<object> ReadProviderJournalAsync() => SeedAsync<object>(async services => {
        await using var database = await services.GetRequiredService<IDbContextFactory<ProviderHistoryDbContext>>().CreateDbContextAsync();
        var rows = await database.Set<HistoryEntryRow>().AsNoTracking()
            .Where(row => row.Granularity == HistoryGranularity.ProviderCallAttempt)
            .Select(row => new { row.Id, row.RequestId, row.AttemptId, row.ProviderId, row.RequestedModel, row.ResolvedModel,
                row.Outcome, row.InputTokens, row.OutputTokens, row.Amount, row.Currency }).ToArrayAsync();
        ProviderJournalRequests = rows.Length;
        return new { count = rows.Length, rows, outboundReservations = requestProxy?.Admitted ?? 0,
            actualOutboundAttempts = requestProxy?.Attempts ?? 0, providerTerminals = requestProxy?.Observations,
            successfulHttpResponses = requestProxy?.Successful ?? 0, budgetRefusals = requestProxy?.Refused ?? 0,
            budgetExecution = requestProxy?.Execution };
    });

    // Spend guard: the journey has no cancellation handle on the server-side run, so a run that exceeds the bound
    // loses its host.
    internal Guid? WatchedAgentId => watchedAgentId == Guid.Empty ? null : watchedAgentId;

    internal void Watch(Guid agentId) {
        if (watchedAgentId != Guid.Empty) {
            throw new InvalidOperationException("The host already owns a watched execution; its bounds cannot be reset.");
        }
        watchedAgentId = agentId;
        watchdog = Task.Run(async () => {
            try {
                while (!stopWatchdog.IsCancellationRequested) {
                    await Task.Delay(TimeSpan.FromSeconds(1), stopWatchdog.Token);
                    int used;
                    try {
                        used = await SeedAsync(async services => (await services.GetRequiredService<IAgentFrameworkWorkspaceService>()
                            .ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: watchedAgentId)))
                            .Sum(run => run.ToolAdmission?.Batches.Length ?? 0));
                    } catch (Exception exception) when (exception is not OperationCanceledException) {
                        WatchdogReadFailures++;
                        WatchdogReadFailureType = exception.GetType().Name;
                        if (requestProxy?.Refused > 0) {
                            await StopAsync(LiveUiStopReason.RequestBudgetRefusal);
                            return;
                        }
                        continue;
                    }
                    ToolAdmissionBatches = Math.Max(ToolAdmissionBatches, used);
                    if (used >= MaximumToolBatchesPerExecution || requestProxy?.Refused > 0) {
                        await StopAsync(requestProxy?.Refused > 0 ? LiveUiStopReason.RequestBudgetRefusal : LiveUiStopReason.ToolBatchLimit);
                        return;
                    }
                }
            } catch (OperationCanceledException) {
            }
        });
    }

    internal Task StopAsync(LiveUiStopReason reason) {
        if (StopReason == LiveUiStopReason.None) {
            StopReason = reason;
        }
        SpendBoundExceeded |= reason is LiveUiStopReason.RequestBudgetRefusal or LiveUiStopReason.ToolBatchLimit;
        return fixture.StopOwnedApplicationAsync();
    }

    internal Task CaptureHostLogAsync() => File.WriteAllTextAsync(Artifact("server.log"), fixture.GetLogSnapshot(int.MaxValue));

    public ValueTask DisposeAsync() => new(disposal ??= DisposeCoreAsync());

    private async Task DisposeCoreAsync() {
        await stopWatchdog.CancelAsync();
        await watchdog;
        var failures = new List<Exception>();
        Func<Task>[] cleanup = [
            () => context?.DisposeAsync().AsTask() ?? Task.CompletedTask,
            async () => {
                if (owner is not null) {
                    await owner.DisposeAsync();
                }
            },
            fixture.DisposeAsync,
            () => requestProxy?.DisposeAsync().AsTask() ?? Task.CompletedTask
        ];
        foreach (var release in cleanup) {
            try {
                await release();
            } catch (Exception failure) {
                failures.Add(failure);
            }
        }
        stopWatchdog.Dispose();
        if (failures.Count > 0) {
            throw new AggregateException("Live UI fixture cleanup failed.", failures);
        }
    }
}
