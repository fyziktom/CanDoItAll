using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class DatasetActivationService(IControlPlanePathResolver paths, DurableFileWriter files,
    IPhysicalFileSystemPathPolicyFactory pathPolicies) {
    internal const int MaximumRecordBytes = 16 * 1024;
    private const int MaximumCachedProfiles = 64;
    private const string ActivationDirectory = "dataset-activations";
    private readonly object cacheSync = new();
    private readonly Dictionary<Guid, DatasetActivationBinding> activeBindings = [];

    public async Task<DatasetActivationBinding> ReadActiveBindingAsync(Guid profileId,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        lock (cacheSync) {
            if (activeBindings.TryGetValue(profileId, out var cached)) {
                return cached;
            }
        }
        using var lease = Acquire(profileId, cancellationToken);
        var record = await lease.ReadAsync(cancellationToken);
        if (record is not { State: DatasetActivationFileState.Active }) {
            throw new InvalidOperationException("The profile has no completed dataset activation; explicit activation or recovery is required.");
        }
        lock (cacheSync) {
            if (activeBindings.Count >= MaximumCachedProfiles && !activeBindings.ContainsKey(profileId)) {
                activeBindings.Remove(activeBindings.Keys.First());
            }
            activeBindings[profileId] = record.Plan.Binding;
        }
        return record.Plan.Binding;
    }

    public DatasetActivationLease Acquire(Guid profileId, CancellationToken cancellationToken = default) {
        if (profileId == Guid.Empty) {
            throw new ArgumentException("A dataset activation lease requires an explicit profile.", nameof(profileId));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var root = paths.ResolveRootPath();
        var directory = Path.Combine(paths.ResolveDatabaseProfilesRootPath(), ActivationDirectory);
        files.EnsureDirectory(root, directory, requirePrivateUnixMode: true);
        var policy = pathPolicies.Create(root);
        var coordination = ControlPlaneFileCoordination.Acquire(files, root,
            ControlPlaneCoordinationScope.DatasetActivation, cancellationToken);
        return new(profileId, root, Path.Combine(directory, $"{profileId:D}.json"), files, policy, coordination, () => {
            lock (cacheSync) {
                activeBindings.Remove(profileId);
            }
        });
    }
}

public sealed class DatasetActivationLease : IDisposable {
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly Guid profileId;
    private readonly string root;
    private readonly string path;
    private readonly DurableFileWriter files;
    private readonly IPhysicalFileSystemPathPolicy pathPolicy;
    private readonly IDisposable coordination;
    private readonly Action invalidateCachedBinding;
    private bool disposed;

    internal DatasetActivationLease(Guid profileId, string root, string path, DurableFileWriter files,
        IPhysicalFileSystemPathPolicy pathPolicy, IDisposable coordination,
        Action invalidateCachedBinding) {
        this.profileId = profileId;
        this.root = root;
        this.path = path;
        this.files = files;
        this.pathPolicy = pathPolicy;
        this.coordination = coordination;
        this.invalidateCachedBinding = invalidateCachedBinding;
    }

    public async Task<DatasetActivationFileRecord?> ReadAsync(CancellationToken cancellationToken = default) {
        RequireActive(cancellationToken);
        pathPolicy.EnsureSafePath(path, allowMissingLeaf: true);
        try {
            if (File.GetAttributes(path).HasFlag(FileAttributes.Directory)) {
                throw new InvalidDataException("The dataset activation path must identify a file.");
            }
        } catch (FileNotFoundException) {
            return null;
        }
        files.HardenPrivateFile(root, path);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > DatasetActivationService.MaximumRecordBytes) {
            throw new InvalidDataException("The dataset activation file exceeds its bounded record size.");
        }
        var bytes = new byte[DatasetActivationService.MaximumRecordBytes + 1];
        var length = 0;
        while (length < bytes.Length) {
            var read = await stream.ReadAsync(bytes.AsMemory(length), cancellationToken);
            if (read == 0) {
                break;
            }
            length += read;
        }
        RequireActive(cancellationToken);
        if (length > DatasetActivationService.MaximumRecordBytes) {
            throw new InvalidDataException("The dataset activation file exceeds its bounded record size.");
        }
        DatasetActivationFileRecord? record;
        try {
            record = JsonSerializer.Deserialize<DatasetActivationFileRecord>(bytes.AsSpan(0, length), SerializerOptions);
        } catch (Exception exception) when (exception is JsonException or ArgumentException) {
            throw new InvalidDataException("The dataset activation file is invalid or unsupported; explicit recovery is required.", exception);
        }
        if (record is null || record.ProfileId != profileId) {
            throw new InvalidDataException("The dataset activation file does not identify the requested profile.");
        }
        return record;
    }

    public async Task WritePreparedAsync(DatasetActivationPlan plan, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(plan);
        RequireActive(cancellationToken);
        invalidateCachedBinding();
        var current = await ReadAsync(cancellationToken);
        if (current?.Plan == plan) {
            return;
        }
        if (current is { State: DatasetActivationFileState.Prepared }) {
            throw new InvalidOperationException("Another dataset activation plan is prepared; resume that exact plan before starting another.");
        }
        if (current is not null && plan.Kind == DatasetActivationKind.Bootstrap) {
            throw new InvalidOperationException("Dataset bootstrap requires an absent activation file.");
        }
        await WriteAsync(new(DatasetActivationFileRecord.CurrentVersion, profileId,
            DatasetActivationFileState.Prepared, plan), createNew: current is null, cancellationToken);
    }

    public async Task ReplaceSupersededBootstrapAsync(Guid expectedPlanId, DatasetActivationPlan attachment,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(attachment);
        RequireActive(cancellationToken);
        var current = await ReadAsync(cancellationToken);
        if (expectedPlanId == Guid.Empty || current is not { State: DatasetActivationFileState.Prepared } ||
                current.Plan.PlanId != expectedPlanId || current.Plan.Kind != DatasetActivationKind.Bootstrap ||
                current.Plan.ExpectedPreviousSqlBinding is not null || attachment.Kind != DatasetActivationKind.Attach ||
                attachment.Binding.Namespace == current.Plan.Binding.Namespace) {
            throw new InvalidOperationException("Only the exact superseded Prepared bootstrap may be replaced by explicit attachment.");
        }
        invalidateCachedBinding();
        await WriteAsync(new(DatasetActivationFileRecord.CurrentVersion, profileId,
            DatasetActivationFileState.Prepared, attachment), createNew: false, cancellationToken);
    }

    public async Task MarkActiveAsync(DatasetActivationPlan plan, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(plan);
        RequireActive(cancellationToken);
        invalidateCachedBinding();
        var current = await ReadAsync(cancellationToken);
        if (current?.Plan != plan) {
            throw new InvalidOperationException("Dataset activation completion requires the exact retained prepared plan.");
        }
        if (current.State == DatasetActivationFileState.Active) {
            return;
        }
        await WriteAsync(new(DatasetActivationFileRecord.CurrentVersion, profileId,
            DatasetActivationFileState.Active, plan), createNew: false, cancellationToken);
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        coordination.Dispose();
    }

    private Task WriteAsync(DatasetActivationFileRecord record, bool createNew, CancellationToken cancellationToken) {
        RequireActive(cancellationToken);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, SerializerOptions);
        if (bytes.Length > DatasetActivationService.MaximumRecordBytes) {
            throw new InvalidDataException("The dataset activation file exceeds its bounded record size.");
        }
        return files.WriteBytesAsync(root, path, bytes, new() {
            RequirePrivateUnixMode = true,
            CommitMode = createNew ? DurableFileCommitMode.CreateNew : DurableFileCommitMode.ReplaceExisting
        }, cancellationToken, beforeCommit: token => {
            RequireActive(token);
            return ValueTask.CompletedTask;
        });
    }

    private void RequireActive(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
