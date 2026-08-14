using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessHostCapabilitySnapshotProvider(
    IEnumerable<IProcessHostCapabilitySource> sources,
    IEnumerable<IProcessHostProfileSource> profileSources) : IProcessHostCapabilitySnapshotProvider
{
    private readonly IReadOnlyList<IProcessHostCapabilitySource> sources =
        (sources ?? throw new ArgumentNullException(nameof(sources))).ToArray();
    private readonly IReadOnlyList<IProcessHostProfileSource> profileSources =
        (profileSources ?? throw new ArgumentNullException(nameof(profileSources))).ToArray();

    public async ValueTask<ProcessHostCapabilitySnapshot> GetAsync(
        CancellationToken cancellationToken = default)
    {
        ValidateSourceOwnership();
        var capabilities = new Dictionary<ProcessHostCapabilityId, ProcessHostCapabilityFact>();
        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyDictionary<ProcessHostCapabilityId, ProcessHostCapabilityFact> sourceCapabilities;
            var failureReason = ProcessHostCapabilityReason.NotRegistered;
            try
            {
                var probed = await source.ProbeAsync(cancellationToken).ConfigureAwait(false);
                sourceCapabilities = TryCreateValidSourceSnapshot(source, probed, out var valid)
                    ? valid
                    : new Dictionary<ProcessHostCapabilityId, ProcessHostCapabilityFact>();
                if (sourceCapabilities.Count == 0 && source.DeclaredCapabilities.Count > 0)
                {
                    failureReason = ProcessHostCapabilityReason.InvalidConfiguration;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                sourceCapabilities = new Dictionary<ProcessHostCapabilityId, ProcessHostCapabilityFact>();
                failureReason = ProcessHostCapabilityReason.TimedOut;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                sourceCapabilities = new Dictionary<ProcessHostCapabilityId, ProcessHostCapabilityFact>();
                failureReason = ProcessHostCapabilityReason.Unavailable;
            }

            foreach (var capabilityId in source.DeclaredCapabilities)
            {
                capabilities.Add(
                    capabilityId,
                    sourceCapabilities.GetValueOrDefault(capabilityId) ??
                    new ProcessHostCapabilityFact(
                        capabilityId,
                        ProcessHostCapabilityAvailability.Unavailable,
                        failureReason,
                        ProcessHostExecutionPort.None));
            }
        }

        var profileId = await ResolveProfileIdAsync(cancellationToken).ConfigureAwait(false);
        return new ProcessHostCapabilitySnapshot(
            profileId,
            capabilities.Values
                .OrderBy(capability => capability.Id.Value, StringComparer.Ordinal)
                .ToArray());
    }

    private void ValidateSourceOwnership()
    {
        var sourceIds = new HashSet<ProcessHostCapabilitySourceId>();
        var capabilityOwners = new Dictionary<ProcessHostCapabilityId, ProcessHostCapabilitySourceId>();
        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source.SourceId.Value) || !sourceIds.Add(source.SourceId))
            {
                throw new InvalidOperationException(
                    "Every process host capability source must declare one unique stable source id.");
            }

            if (source.DeclaredCapabilities is null || source.DeclaredCapabilities.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Process host capability source '{source.SourceId}' must declare at least one owned capability.");
            }

            if (source.DeclaredCapabilities.Count > ProcessHostCapabilitySnapshot.MaximumCapabilities ||
                capabilityOwners.Count + source.DeclaredCapabilities.Count >
                ProcessHostCapabilitySnapshot.MaximumCapabilities)
            {
                throw new InvalidOperationException(
                    $"Process host capability declarations cannot exceed {ProcessHostCapabilitySnapshot.MaximumCapabilities} capabilities in one runtime scope.");
            }

            foreach (var capabilityId in source.DeclaredCapabilities)
            {
                if (string.IsNullOrWhiteSpace(capabilityId.Value) ||
                    !capabilityOwners.TryAdd(capabilityId, source.SourceId))
                {
                    throw new InvalidOperationException(
                        $"Process host capability '{capabilityId}' has invalid or duplicate host-adapter ownership.");
                }
            }
        }
    }

    private static bool TryCreateValidSourceSnapshot(
        IProcessHostCapabilitySource source,
        IReadOnlyList<ProcessHostCapabilityFact>? probed,
        out IReadOnlyDictionary<ProcessHostCapabilityId, ProcessHostCapabilityFact> capabilities)
    {
        var candidate = new Dictionary<ProcessHostCapabilityId, ProcessHostCapabilityFact>();
        if (probed is null ||
            probed.Count > ProcessHostCapabilitySnapshot.MaximumCapabilities ||
            probed.Count > source.DeclaredCapabilities.Count)
        {
            capabilities = candidate;
            return false;
        }

        foreach (var capability in probed)
        {
            if (!source.DeclaredCapabilities.Contains(capability.Id) ||
                !capability.IsStructurallyValid() ||
                !candidate.TryAdd(capability.Id, capability))
            {
                capabilities = new Dictionary<ProcessHostCapabilityId, ProcessHostCapabilityFact>();
                return false;
            }
        }

        capabilities = candidate;
        return true;
    }

    private async ValueTask<ProcessHostProfileId> ResolveProfileIdAsync(
        CancellationToken cancellationToken)
    {
        if (profileSources.Count == 0)
        {
            return ProcessHostCapabilitySnapshot.Unknown.ProfileId;
        }

        if (profileSources.Count > 1)
        {
            throw new InvalidOperationException(
                "More than one process host profile authority is registered for this runtime scope.");
        }

        try
        {
            var profileId = await profileSources[0]
                .GetProfileIdAsync(cancellationToken)
                .ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(profileId.Value)
                ? ProcessHostCapabilitySnapshot.Unknown.ProfileId
                : profileId;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProcessHostCapabilitySnapshot.Unknown.ProfileId;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return ProcessHostCapabilitySnapshot.Unknown.ProfileId;
        }
    }

}

internal sealed class ManagedRuntimeProcessHostCapabilitySource(
    IEnumerable<IWorkspaceProcessHost> processHosts,
    IEnumerable<IMcpClientFactory> mcpClientFactories,
    IEnumerable<WorkspaceExecutableLocator> executableLocators) : IProcessHostCapabilitySource
{
    private static readonly IReadOnlySet<ProcessHostCapabilityId> OwnedCapabilities =
        new HashSet<ProcessHostCapabilityId>
        {
            ProcessHostCapabilityIds.DirectExecution,
            ProcessHostCapabilityIds.LocalStdioMcp,
            ProcessHostCapabilityIds.DotNetRuntime,
            ProcessHostCapabilityIds.PowerShellScript,
            ProcessHostCapabilityIds.PythonRuntime,
            ProcessHostCapabilityIds.NodeRuntime,
            ProcessHostCapabilityIds.NodePackageManager,
            ProcessHostCapabilityIds.PosixScript
        };

    private readonly IWorkspaceProcessHost? processHost = ResolveSingle(processHosts, "workspace process host");
    private readonly IMcpClientFactory? mcpClientFactory = ResolveSingle(mcpClientFactories, "local MCP client factory");
    private readonly WorkspaceExecutableLocator? executableLocator = ResolveSingle(executableLocators, "workspace executable locator");

    public ProcessHostCapabilitySourceId SourceId { get; } = new("managed-runtime");

    public IReadOnlySet<ProcessHostCapabilityId> DeclaredCapabilities => OwnedCapabilities;

    public ValueTask<IReadOnlyList<ProcessHostCapabilityFact>> ProbeAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var processHostAvailable = processHost is not null;
        var localMcpAvailable = mcpClientFactory is not null;
        var capabilities = new List<ProcessHostCapabilityFact>
        {
            CreateRegistrationFact(
                ProcessHostCapabilityIds.DirectExecution,
                processHostAvailable,
                ProcessHostExecutionPort.ManagedProcessHost),
            CreateRegistrationFact(
                ProcessHostCapabilityIds.LocalStdioMcp,
                localMcpAvailable && processHostAvailable,
                ProcessHostExecutionPort.LocalStdioMcpClient),
            ProbeExecutable(ProcessHostCapabilityIds.DotNetRuntime, ["dotnet"]),
            ProbeExecutable(ProcessHostCapabilityIds.PowerShellScript, ["pwsh", "powershell"]),
            ProbeExecutable(ProcessHostCapabilityIds.PythonRuntime, ["python"]),
            ProbeExecutable(ProcessHostCapabilityIds.NodeRuntime, ["node"]),
            ProbeExecutable(ProcessHostCapabilityIds.NodePackageManager, ["npm", "npm.cmd"]),
            ProbeExecutable(ProcessHostCapabilityIds.PosixScript, ["sh"])
        };
        return ValueTask.FromResult<IReadOnlyList<ProcessHostCapabilityFact>>(capabilities);
    }

    private ProcessHostCapabilityFact ProbeExecutable(
        ProcessHostCapabilityId capabilityId,
        IReadOnlyList<string> candidates)
    {
        if (executableLocator is null || processHost is null)
        {
            return CreateRegistrationFact(
                capabilityId,
                false,
                ProcessHostExecutionPort.ManagedProcessHost);
        }

        try
        {
            _ = executableLocator.ResolveExecutablePath(candidates);
            return new ProcessHostCapabilityFact(
                capabilityId,
                ProcessHostCapabilityAvailability.Available,
                ProcessHostCapabilityReason.Ready,
                ProcessHostExecutionPort.ManagedProcessHost);
        }
        catch (WorkspaceExecutableResolutionException exception)
            when (exception.Failure is WorkspaceExecutableResolutionFailure.Missing or
                WorkspaceExecutableResolutionFailure.NotExecutable)
        {
            return new ProcessHostCapabilityFact(
                capabilityId,
                ProcessHostCapabilityAvailability.Unavailable,
                exception.Failure == WorkspaceExecutableResolutionFailure.NotExecutable
                    ? ProcessHostCapabilityReason.PermissionDenied
                    : ProcessHostCapabilityReason.DependencyMissing,
                ProcessHostExecutionPort.None);
        }
        catch (UnauthorizedAccessException)
        {
            return new ProcessHostCapabilityFact(
                capabilityId,
                ProcessHostCapabilityAvailability.Unavailable,
                ProcessHostCapabilityReason.PermissionDenied,
                ProcessHostExecutionPort.None);
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or NotSupportedException)
        {
            return new ProcessHostCapabilityFact(
                capabilityId,
                ProcessHostCapabilityAvailability.Unavailable,
                ProcessHostCapabilityReason.InvalidConfiguration,
                ProcessHostExecutionPort.None);
        }
    }

    private static T? ResolveSingle<T>(IEnumerable<T> registrations, string ownerName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(registrations);
        using var enumerator = registrations.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return null;
        }

        var owner = enumerator.Current;
        if (enumerator.MoveNext())
        {
            throw new InvalidOperationException(
                $"More than one {ownerName} is registered for the process host capability adapter.");
        }

        return owner;
    }

    private static ProcessHostCapabilityFact CreateRegistrationFact(
        ProcessHostCapabilityId capabilityId,
        bool isAvailable,
        ProcessHostExecutionPort executionPort)
        => new(
            capabilityId,
            isAvailable
                ? ProcessHostCapabilityAvailability.Available
                : ProcessHostCapabilityAvailability.Unavailable,
            isAvailable
                ? ProcessHostCapabilityReason.Ready
                : ProcessHostCapabilityReason.NotRegistered,
            isAvailable ? executionPort : ProcessHostExecutionPort.None);
}
