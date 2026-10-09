using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessAuthoringQueryTests(ITestOutputHelper output) {
    [Fact]
    public async Task Thousand_row_catalog_reads_metadata_once_and_records_selected_commit_and_preview_costs() {
        await using var app = await TestApplication.CreateAsync(new());
        var key = await ProcessAuthoringPublicationTests.SeedAsync(app.Services);
        await using var setup = app.Services.CreateAsyncScope();
        var services = setup.ServiceProvider;
        await using var database = await services.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
        var profile = services.GetRequiredService<IProcessAuthoringContext>().DatabaseProfileId;
        database.AuthoringHeads.AddRange(Enumerable.Range(0, 1000).Select(index => new ProcessAuthoringHeadEntity {
            DatabaseProfileId = profile, DefinitionKey = $"scale-{index:D4}", Revision = 1, Lifecycle = ProcessAuthoringLifecycle.Draft,
            Name = $"Scale definition {index:D4}", Summary = "Metadata-only scale fixture", ContentJson = "not-deserialized-by-catalog",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        }));
        await database.SaveChangesAsync();
        using var commands = new Commands(database.Database.GetDbConnection().Database);
        using var subscription = DiagnosticListener.AllListeners.Subscribe(commands);
        for (var sample = 0; sample < 3; sample++) {
            await using var scope = app.Services.CreateAsyncScope();
            var current = scope.ServiceProvider;
            commands.Reset();
            var watch = Stopwatch.StartNew();
            var catalog = await current.GetRequiredService<ProcessDefinitionCatalogProjectionService>().GetCatalogAsync(ProcessWorkspaceShellScope.Global,
                new("Scale definition", null, ProcessDefinitionCatalogScopeKind.All, 50));
            Record("catalog-1001-authored-rows", sample, watch, commands);
            Assert.Equal(50, catalog.Items.Count);
            Assert.Equal(1001, catalog.DraftDefinitionCount);
            Assert.Single(commands.Sql);
            Assert.DoesNotContain("ContentJson", Assert.Single(commands.Sql), StringComparison.Ordinal);

            commands.Reset();
            watch.Restart();
            var workspace = current.GetRequiredService<ProcessAuthoringWorkspace>();
            var selected = await workspace.ReadAsync(ProcessWorkspaceShellScope.Global, new(key), default);
            Record("selected-definition", sample, watch, commands);
            Assert.Single(commands.Sql);

            commands.Reset();
            watch.Restart();
            var committed = await workspace.CommitAsync(selected, Guid.NewGuid(), ProcessAuthoringCodec.Hash($"measured-save-{sample}"), selected.Content,
                ProcessAuthoringLifecycle.Draft, false, null, default);
            await current.GetRequiredService<IProcessAuthoringStore>().ReadAsync(selected.Address);
            Record("durable-save-and-readback", sample, watch, commands);
            Assert.Equal(ProcessAuthoringOutcome.Accepted, committed.Outcome);

            commands.Reset();
            watch.Restart();
            var legacy = new ProcessDefinitionCatalogProjectionService(current.GetRequiredService<ProcessTemplatePackLoader>(), current.GetRequiredService<IProcessProjectionClock>());
            await legacy.GetCatalogAsync(ProcessWorkspaceShellScope.Global, new(null, null, ProcessDefinitionCatalogScopeKind.All, 50));
            Record("distributed-catalog-without-durable-owner", sample, watch, commands);
            Assert.Empty(commands.Sql);
        }
        await ProcessAuthoringPublicationTests.ChangeAsync(app.Services, key, ProcessDefinitionEditorCommandKind.Publish, "Measured publication");
        for (var sample = 0; sample < 3; sample++) {
            await using var scope = app.Services.CreateAsyncScope();
            var current = scope.ServiceProvider;
            var authority = await current.GetRequiredService<IProcessLaunchOperatorAuthoritySource>().CaptureLocalAsync(null, ProcessLaunchOperatorSurface.UserInterface);
            commands.Reset();
            var watch = Stopwatch.StartNew();
            var preview = await current.GetRequiredService<ProcessLaunchApplicationService>().PreviewAsync(new(key, null, null, null, null,
                "query-measurement", new Dictionary<string, string>(), false, false) { Authority = authority, CallerIntentId = new(Guid.NewGuid()) });
            Record("published-preview-and-prepared-commit", sample, watch, commands);
            Assert.NotNull(preview.Observation);
        }
    }

    private void Record(string operation, int sample, Stopwatch watch, Commands commands)
        => output.WriteLine(JsonSerializer.Serialize(new { Operation = operation, Sample = sample, ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds, SqlCommands = commands.Sql.Count }));

    private sealed class Commands(string database) : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable {
        private readonly List<IDisposable> subscriptions = [];
        internal ConcurrentQueue<string> Sql { get; } = new();
        internal void Reset() => Sql.Clear();
        public void OnNext(DiagnosticListener value) {
            if (value.Name == DbLoggerCategory.Name) {
                subscriptions.Add(value.Subscribe(this));
            }
        }
        public void OnNext(KeyValuePair<string, object?> value) {
            if (value.Key == RelationalEventId.CommandExecuting.Name && value.Value is CommandEventData command && command.Command.Connection?.Database == database) {
                Sql.Enqueue(command.Command.CommandText);
            }
        }
        public void OnCompleted() { }
        public void OnError(Exception error) => throw error;
        public void Dispose() {
            foreach (var subscription in subscriptions) {
                subscription.Dispose();
            }
        }
    }
}
