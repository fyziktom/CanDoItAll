using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class FileSandboxWorkspacePreparedRunWitnessTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Large_original_run_is_frozen_before_each_update_overload_and_freshly_hashed_before_commit(bool catalogCallback) {
        await using var fixture = await Fixture.CreateAsync(large: true);
        Assert.True(new FileInfo(fixture.RunPath).Length > FileSandboxWorkspaceSnapshotCache.MaximumPayloadBytes);
        ExecutionRunDetail Change(ExecutionRunDetail previous) {
            Assert.Throws<NotSupportedException>(() => ((IList<PendingToolApprovalRecord>)previous.Run.PendingApprovals).Clear());
            Assert.Equal("approval", Assert.Single(previous.Run.PendingApprovals).ApprovalId);
            return Next(previous);
        }

        var accepted = catalogCallback
            ? await fixture.Store.UpdateExecutionRunDetailAsync(fixture.Before.Run.Id, (_, previous) => Change(previous))
            : await fixture.Store.UpdateExecutionRunDetailAsync(fixture.Before.Run.Id, Change);

        var runReads = fixture.RunReads();
        Assert.Single(runReads, read => read.Kind == FileSandboxWorkspaceJsonReadKind.Deserialization);
        Assert.Single(runReads, read => read.Kind == FileSandboxWorkspaceJsonReadKind.PreparedRunValidation);
        Assert.DoesNotContain(runReads, read => read.Kind == FileSandboxWorkspaceJsonReadKind.RawText);
        Assert.All(runReads, read => Assert.True(read.LengthBytes > FileSandboxWorkspaceSnapshotCache.MaximumPayloadBytes));
        Assert.Equal(fixture.Before.Run.Revision + 1, accepted.Run.Revision);
        Assert.Equal("approval", Assert.Single(accepted.Run.PendingApprovals).ApprovalId);
        Assert.False(File.Exists(fixture.JournalPath));
        var reloaded = await fixture.NewStore().GetExecutionRunDetailAsync(accepted.Run.Id);
        Assert.Equal(fixture.Json(accepted.Run), fixture.Json(reloaded!.Run));
    }

    [Fact]
    public async Task Same_length_timestamp_and_revision_edit_is_rejected_then_original_intent_can_recover() {
        await using var fixture = await Fixture.CreateAsync(large: true);
        var original = await File.ReadAllBytesAsync(fixture.RunPath);
        var timestamp = File.GetLastWriteTimeUtc(fixture.RunPath);
        fixture.OnStage = stage => {
            if (stage == ExistingRunDetailCommitStage.JournalPersisted) {
                File.WriteAllText(fixture.RunPath, fixture.Json(fixture.Before.Run with { Title = "Tampered" }));
                File.SetLastWriteTimeUtc(fixture.RunPath, timestamp);
            }
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.UpdateExecutionRunDetailAsync(fixture.Before.Run.Id, Next));

        Assert.Equal(original.Length, new FileInfo(fixture.RunPath).Length);
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(fixture.RunPath));
        var retained = JsonSerializer.Deserialize<ExecutionRunRecord>(await File.ReadAllTextAsync(fixture.RunPath), Fixture.Options)!;
        Assert.Equal(fixture.Before.Run.Revision, retained.Revision);
        Assert.Equal("Tampered", retained.Title);
        Assert.True(File.Exists(fixture.JournalPath));
        Assert.Equal(2, fixture.RunReads().Count(read => read.Kind == FileSandboxWorkspaceJsonReadKind.Deserialization));
        Assert.Single(fixture.RunReads(), read => read.Kind == FileSandboxWorkspaceJsonReadKind.PreparedRunValidation);

        await File.WriteAllBytesAsync(fixture.RunPath, original);
        fixture.Reads.Clear();
        var recovered = await fixture.NewStore().GetExecutionRunDetailAsync(fixture.Before.Run.Id);
        Assert.Equal(fixture.Before.Run.Revision + 1, recovered!.Run.Revision);
        Assert.Equal("Original", recovered.Run.Title);
        Assert.DoesNotContain(fixture.RunReads(), read => read.Kind == FileSandboxWorkspaceJsonReadKind.PreparedRunValidation);
        Assert.Contains(fixture.RunReads(), read => read.Kind == FileSandboxWorkspaceJsonReadKind.Deserialization);
        Assert.False(File.Exists(fixture.JournalPath));
    }

    [Theory]
    [InlineData(EquivalentEncoding.Compact)]
    [InlineData(EquivalentEncoding.ReorderedWithUnknownProperty)]
    [InlineData(EquivalentEncoding.BomAndWhitespace)]
    public async Task Changed_encoding_of_original_uses_existing_semantic_validation(EquivalentEncoding encoding) {
        await using var fixture = await Fixture.CreateAsync();
        fixture.OnStage = stage => {
            if (stage != ExistingRunDetailCommitStage.JournalPersisted) {
                return;
            }
            var node = JsonNode.Parse(fixture.Json(fixture.Before.Run))!.AsObject();
            if (encoding == EquivalentEncoding.ReorderedWithUnknownProperty) {
                var reordered = new JsonObject();
                foreach (var item in node.Reverse()) {
                    reordered.Add(item.Key, item.Value?.DeepClone());
                }
                reordered["unknownFutureProperty"] = "preserve existing decoder semantics";
                node = reordered;
            }
            var text = node.ToJsonString();
            var bytes = Encoding.UTF8.GetBytes(encoding == EquivalentEncoding.BomAndWhitespace ? $" \r\n{text}\r\n " : text);
            File.WriteAllBytes(fixture.RunPath, encoding == EquivalentEncoding.BomAndWhitespace
                ? [.. Encoding.UTF8.Preamble, .. bytes] : bytes);
        };

        var accepted = await fixture.Store.UpdateExecutionRunDetailAsync(fixture.Before.Run.Id, Next);

        Assert.Equal(2, fixture.RunReads().Count(read => read.Kind == FileSandboxWorkspaceJsonReadKind.Deserialization));
        Assert.Single(fixture.RunReads(), read => read.Kind == FileSandboxWorkspaceJsonReadKind.PreparedRunValidation);
        Assert.Equal(fixture.Json(accepted.Run), await File.ReadAllTextAsync(fixture.RunPath));
        Assert.False(File.Exists(fixture.JournalPath));
    }

    [Fact]
    public async Task Already_written_target_is_compared_and_canonicalized_without_reissuing_revision() {
        await using var fixture = await Fixture.CreateAsync();
        fixture.OnStage = stage => {
            if (stage == ExistingRunDetailCommitStage.JournalPersisted) {
                var journal = JsonSerializer.Deserialize<ExistingRunDetailCommitJournal>(File.ReadAllText(fixture.JournalPath), Fixture.Options)!;
                File.WriteAllText(fixture.RunPath, JsonSerializer.Serialize(journal.PersistencePlan.TargetDetail.Run,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            }
        };

        var accepted = await fixture.Store.UpdateExecutionRunDetailAsync(fixture.Before.Run.Id, Next);

        Assert.Equal(fixture.Before.Run.Revision + 1, accepted.Run.Revision);
        Assert.Single(fixture.RunReads(), read => read.Kind == FileSandboxWorkspaceJsonReadKind.RawText);
        Assert.Equal(fixture.Json(accepted.Run), await File.ReadAllTextAsync(fixture.RunPath));
        Assert.False(File.Exists(fixture.JournalPath));
    }

    [Theory]
    [InlineData(InvalidStoredRun.Missing)]
    [InlineData(InvalidStoredRun.Truncated)]
    [InlineData(InvalidStoredRun.TrailingPayload)]
    public async Task Missing_or_invalid_current_bytes_are_not_treated_as_unchanged(InvalidStoredRun kind) {
        await using var fixture = await Fixture.CreateAsync();
        fixture.OnStage = stage => {
            if (stage != ExistingRunDetailCommitStage.JournalPersisted) {
                return;
            }
            if (kind == InvalidStoredRun.Missing) {
                File.Delete(fixture.RunPath);
            } else {
                File.WriteAllText(fixture.RunPath, kind == InvalidStoredRun.Truncated
                    ? "{\"id\":" : fixture.Json(fixture.Before.Run) + " {}");
            }
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.UpdateExecutionRunDetailAsync(fixture.Before.Run.Id, Next));

        Assert.True(File.Exists(fixture.JournalPath));
        Assert.Equal(kind != InvalidStoredRun.Missing, File.Exists(fixture.RunPath));
        if (kind != InvalidStoredRun.Missing) {
            var retained = await File.ReadAllTextAsync(fixture.RunPath);
            Assert.Equal(kind == InvalidStoredRun.Truncated ? "{\"id\":" : fixture.Json(fixture.Before.Run) + " {}", retained);
        }
    }

    [Fact]
    public async Task Ordinary_save_keeps_full_validation_without_an_update_witness() {
        await using var fixture = await Fixture.CreateAsync(large: true);

        var accepted = await fixture.Store.SaveExecutionRunDetailAsync(Next(fixture.Before));

        Assert.Equal(2, fixture.RunReads().Count(read => read.Kind == FileSandboxWorkspaceJsonReadKind.Deserialization));
        Assert.DoesNotContain(fixture.RunReads(), read => read.Kind == FileSandboxWorkspaceJsonReadKind.PreparedRunValidation);
        Assert.Equal(fixture.Before.Run.Revision + 1, accepted.Run.Revision);
    }

    public enum EquivalentEncoding { Compact, ReorderedWithUnknownProperty, BomAndWhitespace }
    public enum InvalidStoredRun { Missing, Truncated, TrailingPayload }

    private static ExecutionRunDetail Next(ExecutionRunDetail previous) => previous with {
        Run = previous.Run with { Revision = previous.Run.Revision + 1, UpdatedAtUtc = previous.Run.UpdatedAtUtc.AddTicks(1) }
    };

    private sealed class Fixture : IAsyncDisposable {
        internal static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web) { WriteIndented = true };
        private readonly CanDoItAllTestEnvironment environment;
        private readonly string root;
        internal ExecutionRunDetail Before { get; }
        internal FileSandboxWorkspaceStore Store { get; }
        internal ConcurrentQueue<FileSandboxWorkspacePhysicalJsonRead> Reads { get; } = new();
        internal Action<ExistingRunDetailCommitStage>? OnStage { get; set; }
        internal string RunPath { get; }
        internal string JournalPath { get; }

        private Fixture(CanDoItAllTestEnvironment environment, string root, ExecutionRunDetail before) {
            this.environment = environment;
            this.root = root;
            Before = before;
            var layout = new FileSandboxWorkspaceStorageLayout(root, WorkspaceScopeDescriptor.Sandbox);
            RunPath = layout.RunPath(before.Run.Id);
            JournalPath = Path.Combine(layout.ExecutionStorageRoot, "pending-run-detail-update.json");
            Store = new(root, WorkspaceScopeDescriptor.Sandbox, chatBackedRunCommitBoundary: null,
                existingRunDetailCommitBoundary: stage => OnStage?.Invoke(stage),
                jsonReadDiagnostics: new(Reads.Enqueue));
        }

        internal FileSandboxWorkspaceStore NewStore() => new(root, WorkspaceScopeDescriptor.Sandbox,
            chatBackedRunCommitBoundary: null, existingRunDetailCommitBoundary: null, jsonReadDiagnostics: new(Reads.Enqueue));

        internal FileSandboxWorkspacePhysicalJsonRead[] RunReads() =>
            Reads.Where(read => string.Equals(read.FullPath, RunPath, StringComparison.Ordinal)).ToArray();

        internal string Json(ExecutionRunRecord run) => JsonSerializer.Serialize(run, Options);

        internal static async Task<Fixture> CreateAsync(bool large = false) {
            var environment = CanDoItAllTestEnvironment.Create("prepared-run-witness");
            try {
                var profile = environment.CreateInMemoryProfile("primary");
                var setup = new FileSandboxWorkspaceStore(profile.WorkspaceRootPath);
                var catalog = await setup.LoadCatalogSnapshotAsync();
                var agent = catalog.Catalog.Agents.First(candidate => candidate.ProviderProfileId.HasValue);
                var now = DateTimeOffset.UtcNow;
                var run = new ExecutionRunRecord(Guid.NewGuid(), agent.Id, null, "Original", "integration-test", "source",
                    "correlation", "causation", "integration-test", "test", "{}", large ? new string('x', 9 * 1024 * 1024) : "input",
                    string.Empty, "Synthetic", "Synthetic", ExecutionState.Preparing, null, now, now, now, null,
                    string.Empty, null, [new("approval", "call", "tool", "native", "Approve", "{}")]) {
                    ProviderProfileId = agent.ProviderProfileId
                };
                var saved = await setup.SaveExecutionRunDetailAsync(new(run, null, [], []));
                return new(environment, profile.WorkspaceRootPath, saved);
            } catch {
                await environment.DisposeAsync();
                throw;
            }
        }

        public ValueTask DisposeAsync() => environment.DisposeAsync();
    }
}
