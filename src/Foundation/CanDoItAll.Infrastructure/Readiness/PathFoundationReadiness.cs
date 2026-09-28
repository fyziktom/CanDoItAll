using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using System.Text.Json.Serialization;

namespace CanDoItAll.Infrastructure.Readiness;

/// <summary>
/// Readiness of an application path, as a string token: <c>Ready</c> (the root resolved, is writable and passed the
/// path safety checks) or <c>Unavailable</c>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PathFoundationReadinessState
{
    Ready,
    Unavailable
}

/// <summary>
/// Reason for the readiness of an application path, as a string token: <c>Ready</c>, <c>InvalidConfiguration</c>
/// (the root could not be resolved from the configuration), <c>AccessDenied</c> (the operating-system account cannot
/// write it), <c>UnsafePath</c> (the path failed the path safety checks, for example link traversal) or
/// <c>IoFailure</c> (an input/output error while probing it).
/// </summary>
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

/// <summary>
/// Readiness of one application purpose root in a runtime host snapshot: which root, where its location comes from
/// and whether the host can use it. The root's path itself is never reported.
/// </summary>
/// <param name="Purpose">
/// The root, as a string token: <c>Workspace</c>, <c>ControlPlane</c>, <c>DatabaseProfiles</c>,
/// <c>DataProtectionKeys</c>, <c>State</c>, <c>Logs</c> or <c>RuntimeTemporary</c>.
/// </param>
/// <param name="ConfigurationSource">
/// Where the root's location comes from, as a string token: <c>PlatformDefault</c>, <c>ExplicitConfiguration</c>,
/// <c>ActiveDatabaseProfile</c>, <c>DerivedFromControlPlaneRoot</c> or <c>OwnerResolved</c>.
/// </param>
/// <param name="State">Whether the root is usable, as a string token: <c>Ready</c> or <c>Unavailable</c>.</param>
/// <param name="Reason">
/// Why, as a string token: <c>Ready</c>, <c>InvalidConfiguration</c>, <c>AccessDenied</c>, <c>UnsafePath</c> or
/// <c>IoFailure</c>.
/// </param>
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
