using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectProcessReadAuthorityPolicy {
    internal static ProcessLaunchOperatorSurface OperatorSurface(ProcessLaunchAuthority authority)
        => authority.Principal switch {
            ProcessLaunchPrincipal.LocalOperator local => local.Surface,
            ProcessLaunchPrincipal.AuthenticatedOperator => ProcessLaunchOperatorSurface.Api,
            _ => throw new ProcessLaunchAuthorityRejectedException("The Process operator authority has an unsupported source channel.")
        };

    internal static void RequireOperator(ProcessLaunchAuthority authority, ApiAccessOptions options,
        string currentFingerprint, DateTimeOffset now) {
        var surface = OperatorSurface(authority);
        if (authority.PolicyFingerprint != currentFingerprint ||
                surface == ProcessLaunchOperatorSurface.Api && (!options.Enabled ||
                    authority.Principal is ProcessLaunchPrincipal.LocalOperator && options.Authorization.Enabled ||
                    authority.Principal is ProcessLaunchPrincipal.AuthenticatedOperator authenticated &&
                        (!options.Authorization.Enabled || authenticated.ExpiresAtUtc <= now))) {
            throw new ProcessLaunchAuthorityRejectedException("The admitted Process operator authority has expired or its current policy denies the operation.");
        }
    }

    internal static string OperatorFingerprint(ProcessLaunchOperatorSurface surface, ApiAccessOptions? options) {
        if (surface == ProcessLaunchOperatorSurface.UserInterface) {
            return Hash("process-local-ui-v1");
        }
        ArgumentNullException.ThrowIfNull(options);
        return Hash(JsonSerializer.Serialize(new {
            options.Enabled,
            AuthorizationEnabled = options.Authorization.Enabled,
            options.Authorization.Issuer,
            options.Authorization.Audience,
            SigningKeyFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(options.Authorization.SigningKey)))
        }));
    }

    internal static ProcessProjectAdmission? ResolveSourceProject(ProcessLaunchAuthority authority) {
        if (authority.Principal is not ProcessLaunchPrincipal.AgentExecution { Ceiling: var ceiling } ||
                ceiling.WorkspaceScopeKind != ProcessLaunchSourceScopeKind.Project) {
            return null;
        }
        if (ceiling.SourceProjectAdmission is { } source) {
            return source;
        }
        return ceiling.EffectiveSchemaVersion == ProcessLaunchAgentCeiling.LegacySchemaVersion &&
            authority.ProjectAdmission is { } original && Guid.TryParse(ceiling.WorkspaceScopeKey, out var projectId) &&
            original.ProjectId == projectId ? original
            : throw new ProcessLaunchAuthorityRejectedException("The saved Process source has no original project lifetime binding.");
    }

    internal static void RequireSourceProjectGrant(ProcessLaunchAuthority authority, AgentProjectStructureAccessSettings access) {
        if (ResolveSourceProject(authority) is { } source && (!access.CanRead || !access.AllowAllProjects &&
                (!access.AllowedProjectIds.Contains(source.ProjectId) ||
                    !access.AllowedProjectLifetimes.Contains(new(source.DatabaseProfileId, source.ProjectId, source.LifetimeId))))) {
            throw new ProcessLaunchAuthorityRejectedException("Current Agent read policy does not grant the original Process source project lifetime.");
        }
    }

    private static string Hash(string value)
        => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
