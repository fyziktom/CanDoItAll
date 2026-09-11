using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Memory.Abstractions;
using static CanDoItAll.AgentFramework.Core.ToolCapabilityMetadataFactory;

namespace CanDoItAll.Modules.CrmHr;

public static class CrmPlanningToolPolicy {
    internal const string ProjectsSourceKind = "projects";
    internal const string PolicyDeniedCode = "ToolPolicyDenied";
    public const string ProviderKey = "crm.planning-reads";
    public const string Search = "crm_planning_search";
    public const string Summary = "crm_planning_summary_get";
    public const string SearchCapability = "crm-planning-search";
    public const string SummaryCapability = "crm-planning-summary-get";

    public static IReadOnlyList<ToolCapabilityMetadata> Capabilities { get; } = Array.AsReadOnly<ToolCapabilityMetadata>([
        Read(Search, ToolCapabilitySideEffectKind.InternalDataRead) with { ProtectRuntimeStateOnExport = true },
        Read(Summary, ToolCapabilitySideEffectKind.InternalDataRead) with { ProtectRuntimeStateOnExport = true }
    ]);

    internal static string CapabilityKey(string toolName) => toolName switch {
        Search => SearchCapability,
        Summary => SummaryCapability,
        _ => throw new ArgumentOutOfRangeException(nameof(toolName), "Unknown CRM planning operation.")
    };

    internal static bool SupportsSource(string sourceKind)
        => sourceKind is AgentChatTrustedSourceKinds.ProjectStructure or ProjectsSourceKind;

    internal static bool CanAttach(AgentRuntimeToolProviderContext context)
        => context.Purpose == AgentRuntimeToolProviderPurpose.InteractiveChat &&
            context.AdmittedToolSession is { BackgroundSource: null } &&
            context.ToolAdmissionSupport == AgentToolAdmissionSupport.Recoverable &&
            context.Governance is { ReadAllowed: true, WorkspaceScope.Kind: WorkspaceScopeKind.Project } &&
            SupportsSource(context.ContextIntent.SourceKind);

    internal static Guid? ResolveCapability(AgentDefinition actor, IReadOnlyList<CapabilityCatalogItem> catalog,
        AgentExecutionGovernanceSnapshot governance, string toolName) {
        var key = CapabilityKey(toolName);
        if (actor.IsTemplate || actor.Status != AgentLifecycleStatus.Active || !actor.Permissions.CanUseTools ||
                !governance.ReadAllowed || governance.AgentId != actor.Id ||
                governance.AllowedOperations.Count != 0 && !governance.AllowedOperations.Contains(toolName) ||
                governance.AllowedCapabilityKeys.Count != 0 && !governance.AllowedCapabilityKeys.Contains(key) ||
                !AgentMemoryAccessMetadata.Read(actor.ConfigurationJson).AllowedSourceScopes.Contains(MemorySourceScope.Crm)) {
            return null;
        }
        var assignments = actor.Capabilities.Where(item => item.CapabilityKey == key).Take(2).ToArray();
        if (assignments is not [{ Kind: CapabilityKind.Tool } assignment] ||
                catalog.Count(item => item.Id == assignment.CapabilityId && item.Key == key && item.Kind == CapabilityKind.Tool) != 1) {
            return null;
        }
        return assignment.CapabilityId;
    }
}
