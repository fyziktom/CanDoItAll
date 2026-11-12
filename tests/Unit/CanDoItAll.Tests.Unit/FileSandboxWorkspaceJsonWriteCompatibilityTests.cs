using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class FileSandboxWorkspaceJsonWriteCompatibilityTests {
    private static readonly DateTimeOffset RecordedAt = new DateTimeOffset(2026, 10, 16, 12, 34, 56, TimeSpan.Zero).AddTicks(1234567);

    [Fact]
    public async Task Run_writes_preserve_legacy_bytes_runtime_fields_and_previous_content_after_publication_failure() {
        await using var environment = CanDoItAllTestEnvironment.Create("workspace-json-utf8-compatibility");
        var root = Path.Combine(environment.RootPath, "workspace");
        Directory.CreateDirectory(root);
        var factory = new PhysicalFileSystemPathPolicyFactory();
        var writer = new DurableFileWriter(factory);
        var store = new FileSandboxWorkspaceJsonStore(null, factory, writer, root);
        var run = CreateRun();

        await RequireLegacyBytesAsync("typed", run);
        await RequireLegacyBytesAsync<object>("runtime-typed", run);

        var target = Path.Combine(root, "typed.json");
        var previous = await File.ReadAllBytesAsync(target);
        var failure = new IOException("Injected JSON publication failure.");
        var beforeCommit = 0;
        var committed = 0;
        var failingWriter = new DurableFileWriter(factory, stage => {
            if (stage == DurableFileWriteStage.BeforeCommit) {
                beforeCommit++;
                throw failure;
            }
            if (stage == DurableFileWriteStage.Committed) {
                committed++;
            }
        });
        var failingStore = new FileSandboxWorkspaceJsonStore(null, factory, failingWriter, root);

        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() =>
            failingStore.WriteJsonAtomicallyAsync(target, run with { Title = "Changed after capture" }, default)));
        Assert.Equal(1, beforeCommit);
        Assert.Equal(0, committed);
        Assert.Equal(previous, await File.ReadAllBytesAsync(target));
        Assert.Empty(Directory.EnumerateFiles(root, ".*.tmp"));
        Assert.Empty(Directory.EnumerateFiles(root, ".*.pending"));
        var reopened = new FileSandboxWorkspaceJsonStore(null, factory, null, root);
        Assert.Equal(run.Title, (await reopened.ReadJsonAsync<ExecutionRunRecord>(target, default))!.Title);

        async Task RequireLegacyBytesAsync<T>(string name, T payload) {
            var legacyPath = Path.Combine(root, name + ".legacy.json");
            var actualPath = Path.Combine(root, name + ".json");
            await writer.WriteTextAsync(root, legacyPath, JsonSerializer.Serialize(payload, store.SerializerOptions));
            await store.WriteJsonAtomicallyAsync(actualPath, payload, default);
            var actual = await File.ReadAllBytesAsync(actualPath);
            Assert.Equal(await File.ReadAllBytesAsync(legacyPath), actual);
            Assert.False(actual.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            using var document = JsonDocument.Parse(actual);
            Assert.Equal(run.Title, document.RootElement.GetProperty("title").GetString());
            Assert.Equal(run.InitialActivityOperationId!.Value.Value,
                document.RootElement.GetProperty("initialActivityOperationId").GetGuid());
            Assert.Equal(RecordedAt, document.RootElement.GetProperty("createdAtUtc").GetDateTimeOffset());
            var read = await store.ReadJsonAsync<ExecutionRunRecord>(actualPath, default);
            Assert.NotNull(read);
            Assert.Equal(run.InitialActivityOperationId, read.InitialActivityOperationId);
            Assert.Equal(run.MetadataJson, read.MetadataJson);
            Assert.Equal(run.SerializedSessionStateJson, read.SerializedSessionStateJson);
            Assert.Equal(JsonSerializer.Serialize(run, store.SerializerOptions), JsonSerializer.Serialize(read, store.SerializerOptions));
        }
    }

    [Fact]
    public async Task Runtime_typed_usage_preserves_exact_source_bytes_index_and_recoverable_history_after_handoff_failure() {
        await using var environment = CanDoItAllTestEnvironment.Create("workspace-json-usage-compatibility");
        var root = Path.Combine(environment.RootPath, "workspace");
        Directory.CreateDirectory(root);
        var layout = new FileSandboxWorkspaceStorageLayout(root);
        var factory = new PhysicalFileSystemPathPolicyFactory();
        var sourceCommitted = 0;
        var failure = new IOException("Injected history handoff failure after the source committed.");
        var journal = new FileProviderHistoryJournal(layout, stage => {
            if (stage == FileHistoryCommitStage.SourceCommitted) {
                sourceCommitted++;
                throw failure;
            }
        });
        var store = new FileSandboxWorkspaceJsonStore(null, factory, null, root, journal);
        var index = new FileProviderUsageIndex(layout, store);
        store.UsageIndex = index;
        var usage = new ProviderUsageObservation(Guid.NewGuid(), RecordedAt, "Příliš <provider> \"é\"", ProviderKind.OpenAi,
            "模型-🙂", ProviderTransportKind.Responses, ProviderUsageSourcePhases.AgentRuntime,
            ProviderUsageObservationStatus.Observed, 17, 3, 5, 2, 22, 1) {
            ProviderCostUsd = 0.0123456789m,
            RawUsageJson = "{\"text\":\"private-usage-ž\\n🙂\"}",
            DiagnosticsJson = "{\"text\":\"private-diagnostics-漢字\"}"
        };
        var path = Path.Combine(layout.OrphanUsageRoot, $"{usage.Id:N}.json");
        var legacyPath = Path.Combine(root, "usage.legacy.json");
        await new DurableFileWriter(factory).WriteTextAsync(root, legacyPath,
            JsonSerializer.Serialize<object>(usage, store.SerializerOptions));

        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() =>
            store.WriteJsonAtomicallyAsync<object>(path, usage, default)));
        Assert.Equal(1, sourceCommitted);
        var acceptedBytes = await File.ReadAllBytesAsync(path);
        Assert.Equal(await File.ReadAllBytesAsync(legacyPath), acceptedBytes);
        var reopened = new FileSandboxWorkspaceJsonStore(null, factory, null, root);
        Assert.Equal(usage, await reopened.ReadJsonAsync<ProviderUsageObservation>(path, default));
        var currentIndex = new FileProviderUsageIndex(layout, reopened);
        var header = await currentIndex.ReadHeaderAsync(default);
        Assert.NotNull(header);
        var day = DateOnly.FromDateTime(RecordedAt.UtcDateTime);
        var bucket = await reopened.ReadJsonAsync<FileUsageIndexBucket>(currentIndex.BucketPath(header, day), default);
        Assert.NotNull(bucket);
        Assert.Equal(currentIndex.Locator(path), Assert.Single(bucket.Paths));
        Assert.Equal(FileProviderUsageIndex.Hash(bucket), header.Buckets[day]);

        var recovered = new FileProviderHistoryJournal(root);
        var partition = new HistoryPartition(Guid.NewGuid(), Guid.NewGuid(), "utf8-compatibility");
        var publication = Assert.Single(await recovered.ReadBatchAsync(partition, 10));
        Assert.Equal(usage.Id, publication.EvidenceId);
        Assert.Equal(1, publication.Version);
        var entry = Assert.IsType<HistoryEntry>(publication.Mutation.Entry);
        Assert.Equal(usage.ProviderName, entry.Provider.Name);
        Assert.Equal(usage.Model, entry.Provider.RequestedModel!.Value.Value);
        Assert.Equal((long)usage.InputTokens, entry.Usage.InputTokens);
        Assert.Equal((long)usage.OutputTokens, entry.Usage.OutputTokens);
        Assert.Equal(usage.ProviderCostUsd, entry.Price.Amount);
        var projected = JsonSerializer.Serialize(publication, reopened.SerializerOptions);
        Assert.DoesNotContain("private-usage-", projected, StringComparison.Ordinal);
        Assert.DoesNotContain("private-diagnostics-", projected, StringComparison.Ordinal);
        await recovered.AcknowledgeAsync(publication);
        Assert.Empty(await new FileProviderHistoryJournal(root).ReadBatchAsync(partition, 10));
        Assert.Equal(acceptedBytes, await File.ReadAllBytesAsync(path));
    }

    private static ExecutionRunRecord CreateRun() => new(Guid.NewGuid(), Guid.NewGuid(), null,
        "Příliš <run> \"quoted\" & 漢字 🙂\nsecond line", "test", "utf8-compatibility", "correlation", "", "fixture", "test",
        "{\"escaped\":\"é\\n<>&\"}", "Input \t tab", "Completed 🙂", "provider", "model", ExecutionState.Completed,
        RunOutcome.Succeeded, RecordedAt, RecordedAt.AddTicks(3), RecordedAt, RecordedAt.AddTicks(2), "session",
        "{\"state\":\"漢字\"}", []) {
        InitialActivityOperationId = AgentExecutionOperationId.New()
    };
}
