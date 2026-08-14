using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using System.Text.Json.Serialization;

namespace CanDoItAll.Infrastructure.Readiness;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PathFoundationReadinessState
{
    Ready,
    Unavailable
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PathFoundationReadinessReason
{
    Ready,
    InvalidConfiguration,
    AccessDenied,
    UnsafePath,
    IoFailure
}

public sealed record PathCapabilityReadiness(
    PathFoundationReadinessState State,
    PathFoundationReadinessReason Reason);

public sealed record ApplicationPurposeRootReadiness(
    ApplicationPurposeRootKind Purpose,
    ApplicationPurposeRootConfigurationSource ConfigurationSource,
    PathFoundationReadinessState State,
    PathFoundationReadinessReason Reason);

public sealed record PathFoundationReadinessSnapshot(
    PathCapabilityReadiness ControlPlanePaths,
    PathCapabilityReadiness PhysicalFileSystem,
    IReadOnlyList<ApplicationPurposeRootReadiness> PurposeRoots);

public interface IPathFoundationReadinessProbe
{
    PathFoundationReadinessSnapshot Probe();
}

public sealed class PathFoundationReadinessProbe(
    IControlPlanePathResolver controlPlanePathResolver,
    IWorkspacePathResolver workspacePathResolver,
    IPhysicalFileSystemPathPolicyFactory fileSystemPathPolicyFactory) : IPathFoundationReadinessProbe
{
    private static readonly PathCapabilityReadiness Ready = new(
        PathFoundationReadinessState.Ready,
        PathFoundationReadinessReason.Ready);

    public PathFoundationReadinessSnapshot Probe()
    {
        var purposeRoots = new List<ApplicationPurposeRootReadiness>();
        var controlPlaneResults = new List<PathCapabilityReadiness>();
        var fileSystemResults = new List<PathCapabilityReadiness>();
        foreach (RootProbeDefinition definition in ResolvePurposeRoots(
                     controlPlanePathResolver,
                     workspacePathResolver))
        {
            ApplicationPurposeRootConfigurationSource configurationSource =
                ApplicationPurposeRootConfigurationSource.OwnerResolved;
            PathCapabilityReadiness controlPlaneReadiness = Ready;
            PathCapabilityReadiness fileSystemReadiness = Ready;
            string? root = null;
            try
            {
                configurationSource = ResolveConfigurationSource(definition.Owner, definition.Purpose);
                root = definition.ResolvePath();
                VerifyWritable(root);
            }
            catch (Exception exception) when (IsReadinessFailure(exception))
            {
                controlPlaneReadiness = Failure(exception);
                fileSystemReadiness = controlPlaneReadiness;
            }

            if (controlPlaneReadiness.State == PathFoundationReadinessState.Ready)
            {
                try
                {
                    IPhysicalFileSystemPathPolicy policy = fileSystemPathPolicyFactory.Create(root!);
                    policy.EnsureSafePath(root!);
                }
                catch (Exception exception) when (IsReadinessFailure(exception))
                {
                    fileSystemReadiness = Failure(exception);
                }
            }

            controlPlaneResults.Add(controlPlaneReadiness);
            fileSystemResults.Add(fileSystemReadiness);
            PathCapabilityReadiness purposeReadiness =
                controlPlaneReadiness.State == PathFoundationReadinessState.Ready
                    ? fileSystemReadiness
                    : controlPlaneReadiness;
            purposeRoots.Add(new ApplicationPurposeRootReadiness(
                definition.Purpose,
                configurationSource,
                purposeReadiness.State,
                purposeReadiness.Reason));
        }

        return new PathFoundationReadinessSnapshot(
            Aggregate(controlPlaneResults),
            Aggregate(fileSystemResults),
            purposeRoots);
    }

    private static RootProbeDefinition[] ResolvePurposeRoots(
        IControlPlanePathResolver controlPlaneResolver,
        IWorkspacePathResolver workspaceResolver)
        =>
        [
            new(ApplicationPurposeRootKind.Workspace, workspaceResolver, workspaceResolver.ResolveWorkspaceRoot),
            new(ApplicationPurposeRootKind.ControlPlane, controlPlaneResolver, controlPlaneResolver.ResolveRootPath),
            new(ApplicationPurposeRootKind.DatabaseProfiles, controlPlaneResolver, controlPlaneResolver.ResolveDatabaseProfilesRootPath),
            new(ApplicationPurposeRootKind.DataProtectionKeys, controlPlaneResolver, controlPlaneResolver.ResolveDataProtectionKeysPath),
            new(ApplicationPurposeRootKind.State, controlPlaneResolver, controlPlaneResolver.ResolveStateRootPath),
            new(ApplicationPurposeRootKind.Logs, controlPlaneResolver, controlPlaneResolver.ResolveLogsRootPath),
            new(ApplicationPurposeRootKind.RuntimeTemporary, controlPlaneResolver, controlPlaneResolver.ResolveRuntimeTemporaryRootPath)
        ];

    private static ApplicationPurposeRootConfigurationSource ResolveConfigurationSource(
        object owner,
        ApplicationPurposeRootKind purpose)
        => owner is IApplicationPurposeRootConfigurationSource source
            ? source.GetConfigurationSource(purpose)
            : ApplicationPurposeRootConfigurationSource.OwnerResolved;

    private static PathCapabilityReadiness Aggregate(
        IEnumerable<PathCapabilityReadiness> readiness)
        => readiness.FirstOrDefault(item => item.State != PathFoundationReadinessState.Ready) ?? Ready;

    private static void VerifyWritable(string root)
    {
        string probePath = Path.Combine(
            root,
            $".path-readiness-{Guid.NewGuid():N}");
        bool created = false;
        try
        {
            using var stream = new FileStream(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough);
            created = true;
            stream.WriteByte(0);
            stream.Flush(flushToDisk: true);
        }
        finally
        {
            if (created)
            {
                File.Delete(probePath);
            }
        }
    }

    private static PathCapabilityReadiness Failure(Exception exception)
        => new(
            PathFoundationReadinessState.Unavailable,
            exception switch
            {
                UnauthorizedAccessException => PathFoundationReadinessReason.AccessDenied,
                PhysicalPathValidationException => PathFoundationReadinessReason.UnsafePath,
                IOException => PathFoundationReadinessReason.IoFailure,
                _ => PathFoundationReadinessReason.InvalidConfiguration
            });

    private static bool IsReadinessFailure(Exception exception)
        => exception is ArgumentException or
            InvalidOperationException or
            NotSupportedException or
            IOException or
            UnauthorizedAccessException;

    private sealed record RootProbeDefinition(
        ApplicationPurposeRootKind Purpose,
        object Owner,
        Func<string> ResolvePath);
}
