using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectStructureRuntimePathKind
{
    File,
    Directory,
    FileOrDirectory
}

internal sealed record ProjectStructureRuntimePathResolution(
    string? Path,
    bool IsDirectory,
    string Message)
{
    public bool IsSuccess => !string.IsNullOrWhiteSpace(Path);
}

internal sealed class ProjectStructureRuntimePathResolver(
    IWorkspacePathAccessGuard workspacePathAccessGuard,
    IExternalTargetPathRegistryFactory externalTargetPathRegistryFactory,
    FileSystemStoragePathPolicy fileSystemStoragePathPolicy,
    ProjectStructureRuntimeHostContext hostContext)
{
    public ProjectStructureRuntimePathResolution Resolve(
        string value,
        string description,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode,
        ProjectStructureRuntimePathKind pathKind,
        string? basePath = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Fail($"{description} is required.");
        }

        if (!ExternalTargetAliasCodec.IsVersionedAlias(value))
        {
            var resolution = workspacePathAccessGuard.ResolveWorkspacePath(value, basePath);
            if (resolution.IsSuccess)
            {
                return InspectPath(resolution.FullPath, description, pathKind);
            }
        }

        if (TryResolveExistingLocalPath(
                value,
                basePath,
                pathAuthorityMode,
                out var localPath,
                out var localFailureMessage))
        {
            return InspectPath(localPath, description, pathKind);
        }

        return !string.IsNullOrWhiteSpace(localFailureMessage)
            ? Fail($"{description} {localFailureMessage}")
            : Fail($"{description} must stay inside the active workspace root.");
    }

    private ProjectStructureRuntimePathResolution InspectPath(
        string path,
        string description,
        ProjectStructureRuntimePathKind pathKind)
    {
        if (!TryResolveReparseSafePath(path, out var resolvedPath, out var failureMessage))
        {
            return Fail($"{description} {failureMessage}");
        }

        var isDirectory = Directory.Exists(resolvedPath);
        var isFile = File.Exists(resolvedPath);
        if (!isDirectory && !isFile)
        {
            return Fail($"{description} does not exist or is not accessible.");
        }

        if (pathKind == ProjectStructureRuntimePathKind.Directory && !isDirectory)
        {
            return Fail($"{description} must be a directory, but the configured path is a file.");
        }

        if (pathKind == ProjectStructureRuntimePathKind.File && !isFile)
        {
            return Fail($"{description} must be a file, but the configured path is a directory.");
        }

        return new(resolvedPath, isDirectory, "Runtime path resolved.");
    }

    private bool TryResolveExistingLocalPath(
        string value,
        string? basePath,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode,
        out string resolvedPath,
        out string failureMessage)
    {
        resolvedPath = string.Empty;
        failureMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var trimmedValue = value.Trim();
            string candidatePath;
            var isAuthorizedExternalAlias = false;
            if (ExternalTargetAliasCodec.IsVersionedAlias(trimmedValue))
            {
                if (WorkspaceExecutionAuditContext.Current is not { } auditScope)
                {
                    failureMessage = "is an external-target alias without an active execution authority scope.";
                    return false;
                }

                var accessScope = new EffectiveExternalTargetAccessScope(
                    auditScope.AllowedExternalTargetAliases,
                    auditScope.ReadOnlyExternalTargetAliases);
                if (!accessScope.CanRead(trimmedValue))
                {
                    failureMessage = "is not authorized for the current execution.";
                    return false;
                }

                var externalTargets = externalTargetPathRegistryFactory.Create(
                    auditScope.ExternalTargetRootBindings);
                if (externalTargets.TryResolve(
                        trimmedValue,
                        out candidatePath,
                        out var aliasValidationMessage) != ExternalTargetAliasResolutionKind.Resolved)
                {
                    failureMessage = string.IsNullOrWhiteSpace(aliasValidationMessage)
                        ? "could not be resolved on this host."
                        : aliasValidationMessage;
                    return false;
                }

                isAuthorizedExternalAlias = true;
            }
            else
            {
                var syntax = PhysicalPathSyntaxClassifier.Classify(trimmedValue);
                if (IsForeignOrUnsupported(syntax))
                {
                    failureMessage = "uses path syntax that is not valid for this host.";
                    return false;
                }

                if (syntax != PhysicalPathSyntax.Relative)
                {
                    candidatePath = Path.GetFullPath(trimmedValue);
                }
                else if (!string.IsNullOrWhiteSpace(basePath))
                {
                    candidatePath = Path.GetFullPath(Path.Combine(basePath, trimmedValue));
                }
                else
                {
                    return false;
                }
            }

            if (!isAuthorizedExternalAlias &&
                !CanCurrentExecutionReadPath(candidatePath, pathAuthorityMode))
            {
                failureMessage = pathAuthorityMode == ProjectStructureRuntimePathAuthorityMode.AgentExecution
                    ? "is outside the active workspace and is not authorized for this agent execution."
                    : "is outside the active workspace and is not authorized for the current execution.";
                return false;
            }

            if (!TryResolveReparseSafePath(candidatePath, out resolvedPath, out failureMessage))
            {
                return false;
            }

            if (!Directory.Exists(resolvedPath) && !File.Exists(resolvedPath))
            {
                failureMessage = "does not exist or is not accessible.";
                resolvedPath = string.Empty;
                return false;
            }

            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            failureMessage = "could not be resolved safely on this host.";
            return false;
        }
    }

    private bool IsForeignOrUnsupported(PhysicalPathSyntax syntax)
        => syntax == PhysicalPathSyntax.Uri ||
           (hostContext.Platform == ProjectStructureRuntimeHostPlatform.Windows
               ? syntax == PhysicalPathSyntax.UnixAbsolute
               : syntax is PhysicalPathSyntax.WindowsDriveAbsolute or
                   PhysicalPathSyntax.WindowsDriveRelative or
                   PhysicalPathSyntax.WindowsUnc or
                   PhysicalPathSyntax.WindowsDevice);

    private bool TryResolveReparseSafePath(
        string path,
        out string resolvedPath,
        out string failureMessage)
    {
        try
        {
            resolvedPath = fileSystemStoragePathPolicy.ResolveReparseSafeFullPath(path);
            failureMessage = string.Empty;
            return true;
        }
        catch (StorageBrowseException)
        {
            resolvedPath = string.Empty;
            failureMessage = "cannot traverse symbolic links or filesystem reparse points.";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            resolvedPath = string.Empty;
            failureMessage = "is not accessible to the current process.";
            return false;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException)
        {
            resolvedPath = string.Empty;
            failureMessage = "could not be inspected safely.";
            return false;
        }
    }

    private bool CanCurrentExecutionReadPath(
        string candidatePath,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        if (workspacePathAccessGuard.ResolveWorkspacePath(candidatePath).IsSuccess)
        {
            return true;
        }

        if (WorkspaceExecutionAuditContext.Current is not { } auditScope)
        {
            return pathAuthorityMode == ProjectStructureRuntimePathAuthorityMode.OperatorSelected;
        }

        var externalTargets = externalTargetPathRegistryFactory.Create(
            auditScope.ExternalTargetRootBindings);
        if (!externalTargets.TryCreateAlias(candidatePath, out var candidateAlias))
        {
            return false;
        }

        return new EffectiveExternalTargetAccessScope(
                auditScope.AllowedExternalTargetAliases,
                auditScope.ReadOnlyExternalTargetAliases)
            .CanRead(candidateAlias);
    }

    private static ProjectStructureRuntimePathResolution Fail(string message)
        => new(null, false, message);
}
