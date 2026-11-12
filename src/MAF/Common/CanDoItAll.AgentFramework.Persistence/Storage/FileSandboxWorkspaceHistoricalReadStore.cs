using System.Collections.Immutable;
using System.IO.Enumeration;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed partial class FileSandboxWorkspaceHistoricalReadStore : ISandboxWorkspaceHistoricalReadStore, IAgentHistoricalCatalogReadStore {
    private static readonly TimeSpan MaximumReadDuration = TimeSpan.FromSeconds(5);
    private readonly FileSandboxWorkspaceStorageLayout layout;
    private readonly IPhysicalFileSystemPathPolicy pathPolicy;
    private readonly TimeProvider clock;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] PendingJournalNames = ["pending-chat-run-start.json", "pending-run-start.json",
        "pending-run-detail-update.json", "pending-agent-deletion.json"];

    public FileSandboxWorkspaceHistoricalReadStore(string workspaceRoot, WorkspaceScopeDescriptor scope)
        : this(workspaceRoot, scope, TimeProvider.System) {
    }

    internal FileSandboxWorkspaceHistoricalReadStore(string workspaceRoot, WorkspaceScopeDescriptor scope, TimeProvider clock) {
        ArgumentNullException.ThrowIfNull(scope);
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        pathPolicy = new PhysicalFileSystemPathPolicyFactory().Create(workspaceRoot, PhysicalFileSystemCaseSensitivity.Sensitive);
        layout = new(pathPolicy.RootPath, scope);
    }
    internal const long HistoricalRawDocumentLimit = 64L * 1024 * 1024;
    internal const long HistoricalChatScanLimit = 256L * 1024 * 1024;
    private const long HistoricalIndexLimit = 64 * 1024;

    public async Task<AgentHistoricalRunRead> ReadHistoricalRunAsync(Guid executionRunId, CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfEqual(executionRunId, Guid.Empty);
        using var budget = new HistoricalReadBudget(clock, cancellationToken);
        try {
            var pass = new HistoricalReadPass(this, budget, LayoutTargets().Append(layout.RunPath(executionRunId)));
            var before = await ReadHistoricalReadyIndexAsync(pass, budget.Token);
            var (run, digest) = await ReadHistoricalFileAsync<ExecutionRunRecord>(layout.RunPath(executionRunId), HistoricalRawDocumentLimit, pass, budget.Token);
            if (run is null) {
                await RequireHistoricalIndexAsync(before, pass, budget);
                return new(AgentHistoricalStorageReadStatus.Missing);
            }
            AgentHistoricalChatIdentity? chat = null;
            if (run.ChatSessionId is { } chatId) {
                chat = await ReadHistoricalChatAsync(layout.SessionPath(chatId), pass, budget.Token);
                if (chat is null || chat.Id != chatId || chat.AgentId != run.AgentId) {
                    return new(AgentHistoricalStorageReadStatus.UnsupportedLayout);
                }
            }
            await RequireHistoricalIndexAsync(before, pass, budget);
            budget.Require();
            return new(AgentHistoricalStorageReadStatus.Ready, new(run, chat, digest));
        } catch (HistoricalStorageRefusal refusal) {
            cancellationToken.ThrowIfCancellationRequested();
            return new(refusal.Status);
        } catch (OperationCanceledException) when (budget.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            return new(AgentHistoricalStorageReadStatus.ReadBudgetExceeded);
        }
    }

    public async Task<AgentHistoricalAgentRead> ReadHistoricalAgentAsync(Guid agentId, CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, Guid.Empty);
        using var budget = new HistoricalReadBudget(clock, cancellationToken);
        try {
            var pass = new HistoricalReadPass(this, budget, LayoutTargets());
            var before = await ReadHistoricalReadyIndexAsync(pass, budget.Token);
            var (catalog, digest) = await ReadHistoricalFileAsync<HistoricalCatalog>(layout.CatalogPath, HistoricalRawDocumentLimit, pass, budget.Token);
            if (catalog is null || !catalog.CatalogDataRevision.IsAssigned || catalog.Agents is null) {
                return new(AgentHistoricalStorageReadStatus.UnsupportedLayout, layout.Scope);
            }
            AgentDefinition? selected = null;
            foreach (var agent in catalog.Agents) {
                budget.Require();
                if (agent is null) {
                    throw new InvalidDataException("The historical catalog contains an invalid Agent entry.");
                }
                if (agent.Id != agentId) {
                    continue;
                }
                if (selected is not null) {
                    throw new InvalidDataException("The historical catalog contains duplicate Agent identities.");
                }
                selected = agent with {
                    Permissions = agent.Permissions with { AllowedSecrets = agent.Permissions.NormalizedAllowedSecrets.ToImmutableArray() },
                    Capabilities = agent.Capabilities.ToImmutableArray(), Tags = agent.Tags.ToImmutableArray()
                };
            }
            await RequireHistoricalIndexAsync(before, pass, budget);
            budget.Require();
            return new(selected is null ? AgentHistoricalStorageReadStatus.Missing : AgentHistoricalStorageReadStatus.Ready,
                layout.Scope, catalog.CatalogDataRevision, selected, digest);
        } catch (HistoricalStorageRefusal refusal) {
            cancellationToken.ThrowIfCancellationRequested();
            return new(refusal.Status, layout.Scope);
        } catch (OperationCanceledException) when (budget.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            return new(AgentHistoricalStorageReadStatus.ReadBudgetExceeded, layout.Scope);
        }
    }

    private async Task<WorkspaceStorageIndex> ReadHistoricalReadyIndexAsync(HistoricalReadPass pass, CancellationToken cancellationToken) {
        RequireHistoricalLayout(pass);
        var (index, _) = await ReadHistoricalFileAsync<WorkspaceStorageIndex>(layout.WorkspaceIndexPath, HistoricalIndexLimit, pass, cancellationToken);
        return index is { Revision: > 0 } ? index : throw new HistoricalStorageRefusal(AgentHistoricalStorageReadStatus.UnsupportedLayout);
    }

    private async Task RequireHistoricalIndexAsync(WorkspaceStorageIndex original, HistoricalReadPass before, HistoricalReadBudget budget) {
        var after = new HistoricalReadPass(this, budget, before.Targets);
        var current = await ReadHistoricalReadyIndexAsync(after, budget.Token);
        budget.Require();
        if (current != original) {
            throw new HistoricalStorageRefusal(AgentHistoricalStorageReadStatus.SnapshotChanged);
        }
    }

    private IEnumerable<string> LayoutTargets() => PendingJournalNames
        .Select(name => Path.Combine(layout.ExecutionStorageRoot, name))
        .Concat([layout.CatalogPath, layout.WorkspaceIndexPath, layout.ExecutionIndexPath]);

    private void RequireHistoricalLayout(HistoricalReadPass pass) {
        foreach (var name in PendingJournalNames) {
            if (pass.FileExists(Path.Combine(layout.ExecutionStorageRoot, name))) {
                throw new HistoricalStorageRefusal(AgentHistoricalStorageReadStatus.RecoveryRequired);
            }
        }
        if (!pass.FileExists(layout.CatalogPath) || !pass.FileExists(layout.WorkspaceIndexPath) ||
            !pass.FileExists(layout.ExecutionIndexPath)) {
            throw new HistoricalStorageRefusal(AgentHistoricalStorageReadStatus.UnsupportedLayout);
        }
    }

    private sealed class HistoricalReadPass {
        private readonly FileSandboxWorkspaceHistoricalReadStore owner;
        private readonly HistoricalReadBudget budget;
        private readonly Dictionary<string, HashSet<string>> targetsByParent = new(StringComparer.Ordinal);
        internal HashSet<string> Targets { get; } = new(StringComparer.Ordinal);

        internal HistoricalReadPass(FileSandboxWorkspaceHistoricalReadStore owner, HistoricalReadBudget budget, IEnumerable<string> targets) {
            this.owner = owner;
            this.budget = budget;
            RequireTargets(targets);
        }

        internal bool FileExists(string path) {
            RequireNoPending(path);
            try {
                if ((File.GetAttributes(path) & FileAttributes.Directory) != 0) {
                    throw new HistoricalStorageRefusal(AgentHistoricalStorageReadStatus.UnsupportedLayout);
                }
                return true;
            } catch (FileNotFoundException) {
                return false;
            } catch (DirectoryNotFoundException) {
                return false;
            }
        }

        internal void RequireNoPending(string path) {
            RequireTargets([path]);
        }

        private void RequireTargets(IEnumerable<string> paths) {
            budget.Require();
            var changedParents = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in paths) {
                owner.pathPolicy.EnsureSafePath(path, allowMissingLeaf: true);
                if (!Targets.Add(path)) {
                    continue;
                }
                var parent = Path.GetDirectoryName(path) ?? throw new InvalidDataException("Historical target has no parent.");
                if (!targetsByParent.TryGetValue(parent, out var targets)) {
                    targets = new(StringComparer.Ordinal);
                    targetsByParent.Add(parent, targets);
                }
                targets.Add(Path.GetFileName(path));
                changedParents.Add(parent);
            }
            foreach (var parent in changedParents) {
                budget.Require();
                owner.pathPolicy.EnsureSafePath(parent, allowMissingLeaf: true);
                var visited = 0L;
                try {
                    var entries = new FileSystemEnumerable<string>(parent, (ref FileSystemEntry entry) => entry.ToFullPath(),
                        new EnumerationOptions { RecurseSubdirectories = false, IgnoreInaccessible = false, AttributesToSkip = 0 }) {
                        ShouldIncludePredicate = (ref FileSystemEntry entry) => {
                            budget.Require();
                            visited++;
                            foreach (var target in targetsByParent[parent]) {
                                if (DurableFileWriter.IsPendingPublicationFileName(entry.FileName, target.AsSpan(), StringComparison.OrdinalIgnoreCase)) {
                                    return true;
                                }
                            }
                            return false;
                        }
                    };
                    using var enumerator = entries.GetEnumerator();
                    while (enumerator.MoveNext()) {
                        owner.pathPolicy.EnsureSafePath(enumerator.Current);
                        throw new HistoricalStorageRefusal(AgentHistoricalStorageReadStatus.RecoveryRequired);
                    }
                } catch (DirectoryNotFoundException) when (visited == 0) {
                }
                budget.Require();
            }
        }
    }

    private sealed class HistoricalReadBudget : IDisposable {
        private readonly TimeProvider clock;
        private readonly long started;
        private readonly CancellationToken caller;
        private readonly CancellationTokenSource cancellation;

        internal HistoricalReadBudget(TimeProvider clock, CancellationToken caller) {
            this.clock = clock;
            this.caller = caller;
            started = clock.GetTimestamp();
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(caller);
            cancellation.CancelAfter(MaximumReadDuration);
        }

        internal CancellationToken Token => cancellation.Token;

        internal void Require() {
            caller.ThrowIfCancellationRequested();
            if (cancellation.IsCancellationRequested || clock.GetElapsedTime(started) >= MaximumReadDuration) {
                throw new HistoricalStorageRefusal(AgentHistoricalStorageReadStatus.ReadBudgetExceeded);
            }
        }

        public void Dispose() => cancellation.Dispose();
    }

    private async Task<(T? Value, AgentToolSemanticDigest Digest)> ReadHistoricalFileAsync<T>(string path, long maximumBytes,
        HistoricalReadPass pass, CancellationToken cancellationToken) where T : class {
        if (!pass.FileExists(path)) {
            return (null, default);
        }
        using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.historical.read");
        activity?.SetTag("payload.type", typeof(T).Name);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            bufferSize: 16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (file.Length > maximumBytes) {
            throw new HistoricalStorageRefusal(AgentHistoricalStorageReadStatus.SizeLimitExceeded);
        }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var bounded = new HistoricalReadStream(file, maximumBytes, hash);
        T? value;
        try {
            value = await JsonSerializer.DeserializeAsync<T>(bounded, SerializerOptions, cancellationToken);
        } finally {
            activity?.SetTag("payload.bytes", bounded.Position);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return (value, new(Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()));
    }

    private sealed record HistoricalCatalog(CatalogDataRevision CatalogDataRevision, IReadOnlyList<AgentDefinition>? Agents);

    private sealed class HistoricalStorageRefusal(AgentHistoricalStorageReadStatus status) : IOException {
        internal AgentHistoricalStorageReadStatus Status { get; } = status;
    }

    private sealed class HistoricalReadStream(Stream source, long maximumBytes, IncrementalHash hash) : Stream {
        private long consumed;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => consumed; set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) {
            var read = source.Read(buffer, offset, Math.Min(count, checked((int)Math.Min(int.MaxValue, maximumBytes - consumed + 1))));
            Observe(buffer.AsSpan(offset, read));
            return read;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
            var allowed = Math.Min(buffer.Length, checked((int)Math.Min(int.MaxValue, maximumBytes - consumed + 1)));
            var read = await source.ReadAsync(buffer[..allowed], cancellationToken);
            Observe(buffer.Span[..read]);
            return read;
        }
        private void Observe(ReadOnlySpan<byte> value) {
            consumed = checked(consumed + value.Length);
            if (consumed > maximumBytes) {
                throw new HistoricalStorageRefusal(AgentHistoricalStorageReadStatus.SizeLimitExceeded);
            }
            hash.AppendData(value);
        }
    }
}
