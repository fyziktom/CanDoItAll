using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class WorkspaceRuntimeFileAccessGuard(
    string workspaceRoot,
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
    WorkspaceScopeDescriptor workspaceScope,
    AgentWorkspaceToolAccessSettings accessSettings)
{
    private readonly IPhysicalFileSystemPathPolicy workspacePathPolicy =
        physicalPathPolicyFactory.Create(NormalizeWorkspaceRoot(workspaceRoot));
    private readonly WorkspaceScopeDescriptor workspaceScope = workspaceScope;
    private readonly AgentWorkspaceToolAccessSettings accessSettings = AgentWorkspaceToolAccessMetadata.Normalize(accessSettings);

    public string? PrepareFileReadPath(string? path)
    {
        EnsureFileReadAllowed(path);
        return NormalizeRecoverableCurrentRunArtifactPath(NormalizeAllowedExternalPath(path));
    }

    public string? PrepareFileWritePath(string? path)
    {
        EnsureFileWriteAllowed(path);
        return NormalizeAllowedExternalPath(path);
    }

    public void EnsureFileReadAllowed(string? path)
    {
        if (!accessSettings.CanReadFiles && !accessSettings.CanWriteFiles)
        {
            throw WorkspaceToolAccessDeniedException.FileReadDisabled();
        }

        EnsureExternalAliasAllowed(path, requireWrite: false);
    }

    public void EnsureFileWriteAllowed(string? path)
    {
        if (!accessSettings.CanWriteFiles)
        {
            throw WorkspaceToolAccessDeniedException.FileWriteDisabled();
        }

        EnsureExternalAliasAllowed(path, requireWrite: true);
    }

    public void EnsureExternalAliasAllowed(string? path, bool requireWrite)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (IsManagedWorkspaceAbsolutePath(path))
        {
            return;
        }

        var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(path);
        if (string.IsNullOrWhiteSpace(normalizedAlias))
        {
            if (PhysicalPathSyntaxPolicy.Classify(path) == PhysicalPathSyntax.Relative)
            {
                return;
            }

            throw WorkspaceToolAccessDeniedException.ExternalTargetNotAuthorized(
                "external-target/unresolved");
        }

        var externalAccess = ResolveExternalTargetAccess();
        if (requireWrite && externalAccess.CanWrite(normalizedAlias))
        {
            return;
        }

        if (!requireWrite && externalAccess.CanRead(normalizedAlias))
        {
            return;
        }

        if (requireWrite && externalAccess.CanRead(normalizedAlias))
        {
            throw WorkspaceToolAccessDeniedException.ExternalTargetReadOnly(normalizedAlias);
        }

        throw WorkspaceToolAccessDeniedException.ExternalTargetNotAuthorized(normalizedAlias);
    }

    public string? NormalizeAllowedExternalPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsManagedWorkspaceAbsolutePath(path))
        {
            return path;
        }

        var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(path);
        return string.IsNullOrWhiteSpace(normalizedAlias)
            ? path
            : normalizedAlias;
    }

    public EffectiveExternalTargetAccessScope ResolveExternalTargetAccess()
    {
        var auditScope = WorkspaceExecutionAuditContext.Current;
        return EffectiveExternalTargetAccessResolver.Resolve(
            accessSettings,
            auditScope?.AllowedExternalTargetAliases,
            auditScope?.ReadOnlyExternalTargetAliases,
            auditScope?.InvocationExternalTargetScopeIsAuthoritative == true);
    }

    public bool IsManagedWorkspaceAbsolutePath(string path)
    {
        try
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(path, "MAF managed-workspace candidate path");
            if (!Path.IsPathRooted(path))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(path);
            return workspacePathPolicy.IsWithinRoot(fullPath);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private string? NormalizeRecoverableCurrentRunArtifactPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var auditScope = WorkspaceExecutionAuditContext.Current;
        var currentRunId = auditScope?.ProcessRunId;
        var currentWorkspaceScope = auditScope?.ContextWorkspaceScope ?? workspaceScope;
        return WorkspaceProcessRunArtifactPath.TryBuildRecoverableCurrentRunPath(
            path,
            currentRunId,
            currentWorkspaceScope,
            out var currentRunPath)
            ? currentRunPath
            : path;
    }

    private static string NormalizeWorkspaceRoot(string path)
    {
        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(path, "MAF workspace root");
        return Path.GetFullPath(path);
    }

}
