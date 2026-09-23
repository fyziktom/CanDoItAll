using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.Hosting;

using ProviderConnectorDefaults = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorDefaults;

public sealed class ProcessMockAgentOptions
{
    public const string SectionName = "AgentFramework:ProcessMockAgents";

    public bool Enabled { get; set; }
}

public static class ProcessMockAgentCatalog
{
    public const string ProviderBaseUrl = ProviderConnectorDefaults.ProcessMockBaseUrl;
    public const string ProviderName = "Process Mock Agent Provider";
    public const string Model = ProviderConnectorDefaults.ProcessMockModel;
    public const string AgentTag = "process-mock-agent";
    public const string RoleTagPrefix = "process-mock-role:";
    public const string BranchRepairsRequired = "repairs-required";
    public const string BranchApproved = "approved";
    public const string ProcessSourceKind = "process-step";
    public const string ArtifactRoot = "artifacts/process-mock";
    public const string OutputRoot = "output/process-mock";

    public static IReadOnlyList<ProcessMockAgentRoleDefinition> Roles { get; } =
    [
        new(
            ProcessMockAgentRoleKeys.ProductOwner,
            ProcessMockAgentRolePartyIds.ProductOwner,
            "Process Mock Product Owner",
            "Product Owner",
            "Writes deterministic mock scope and acceptance criteria.",
            "Write the mock delivery scope artifact and preserve clear acceptance criteria for downstream agents.",
            AgentWorkloadKind.Management),
        new(
            ProcessMockAgentRoleKeys.Architect,
            ProcessMockAgentRolePartyIds.Architect,
            "Process Mock Architect",
            "Solution Architect",
            "Writes deterministic mock architecture guidance.",
            "Write concise architecture notes for a small validation component and pass concrete constraints to implementation.",
            AgentWorkloadKind.Programming),
        new(
            ProcessMockAgentRoleKeys.Developer,
            ProcessMockAgentRolePartyIds.Developer,
            "Process Mock Developer",
            "Developer",
            "Writes the first deterministic mock implementation artifact.",
            "Produce the first mock implementation artifact. This mock intentionally leaves one QA defect so repair-loop testing is deterministic.",
            AgentWorkloadKind.Programming),
        new(
            ProcessMockAgentRoleKeys.Qa,
            ProcessMockAgentRolePartyIds.Qa,
            "Process Mock QA",
            "QA Reviewer",
            "Rejects the first mock implementation and approves the repaired version.",
            "Review sample artifacts deterministically. Reject the first implementation for repair, then approve the repaired implementation.",
            AgentWorkloadKind.Qa),
        new(
            ProcessMockAgentRoleKeys.RepairDeveloper,
            ProcessMockAgentRolePartyIds.RepairDeveloper,
            "Process Mock Repair Developer",
            "Repair Developer",
            "Writes the deterministic sample repair artifact.",
            "Repair the mock implementation according to QA findings and preserve a clear repair note.",
            AgentWorkloadKind.Programming),
        new(
            ProcessMockAgentRoleKeys.ReleaseManager,
            ProcessMockAgentRolePartyIds.ReleaseManager,
            "Process Mock Release Manager",
            "Release Manager",
            "Writes deterministic release notes after QA approval.",
            "Prepare release notes that summarize scope, repair evidence, QA approval, and residual risk.",
            AgentWorkloadKind.Management),
        new(
            ProcessMockAgentRoleKeys.BusinessStrategist,
            ProcessMockAgentRolePartyIds.BusinessStrategist,
            "Process Mock Business Strategist",
            "Business Strategist",
            "Writes deterministic business strategy, product evidence, plan, and review artifacts.",
            "Prepare governed business planning artifacts that separate facts, assumptions, risks, and next actions.",
            AgentWorkloadKind.Management),
        new(
            ProcessMockAgentRoleKeys.FinancialStrategist,
            ProcessMockAgentRolePartyIds.FinancialStrategist,
            "Process Mock Financial Strategist",
            "Financial Strategist",
            "Writes deterministic financial model and sensitivity artifacts.",
            "Prepare financial planning evidence with drivers, ranges, assumptions, and data gaps.",
            AgentWorkloadKind.Spreadsheet),
        new(
            ProcessMockAgentRoleKeys.MarketingSpecialist,
            ProcessMockAgentRolePartyIds.MarketingSpecialist,
            "Process Mock Marketing Specialist",
            "Marketing Specialist",
            "Writes deterministic go-to-market and experiment planning artifacts.",
            "Prepare marketing planning evidence with audience, promise, channels, metrics, and experiments.",
            AgentWorkloadKind.Sales)
    ];

    public static string CreateRoleTag(string roleKey)
        => RoleTagPrefix + roleKey;

    public static string? ResolveRoleKey(AgentDefinition agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var roleTag = agent.Tags.FirstOrDefault(item =>
            item.StartsWith(RoleTagPrefix, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(roleTag)
            ? null
            : roleTag[RoleTagPrefix.Length..].Trim();
    }

    public static bool IsProcessMockProvider(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return string.Equals(provider.BaseUrl, ProviderBaseUrl, StringComparison.OrdinalIgnoreCase);
    }
}

public static class ProcessMockAgentRoleKeys
{
    public const string ProductOwner = "product-owner";
    public const string Architect = "architect";
    public const string Developer = "developer";
    public const string Qa = "qa";
    public const string RepairDeveloper = "repair-developer";
    public const string ReleaseManager = "release-manager";
    public const string BusinessStrategist = "business-strategist";
    public const string FinancialStrategist = "financial-strategist";
    public const string MarketingSpecialist = "marketing-specialist";
}

public static class ProcessMockAgentRolePartyIds
{
    public static readonly Guid ProductOwner = Guid.Parse("3f540e6d-5b9e-49e6-9ab4-8ff6b4fd1001");
    public static readonly Guid Architect = Guid.Parse("3f540e6d-5b9e-49e6-9ab4-8ff6b4fd1002");
    public static readonly Guid Developer = Guid.Parse("3f540e6d-5b9e-49e6-9ab4-8ff6b4fd1003");
    public static readonly Guid Qa = Guid.Parse("3f540e6d-5b9e-49e6-9ab4-8ff6b4fd1004");
    public static readonly Guid RepairDeveloper = Guid.Parse("3f540e6d-5b9e-49e6-9ab4-8ff6b4fd1005");
    public static readonly Guid ReleaseManager = Guid.Parse("3f540e6d-5b9e-49e6-9ab4-8ff6b4fd1006");
    public static readonly Guid BusinessStrategist = Guid.Parse("3f540e6d-5b9e-49e6-9ab4-8ff6b4fd1007");
    public static readonly Guid FinancialStrategist = Guid.Parse("3f540e6d-5b9e-49e6-9ab4-8ff6b4fd1008");
    public static readonly Guid MarketingSpecialist = Guid.Parse("3f540e6d-5b9e-49e6-9ab4-8ff6b4fd1009");
}

public sealed record ProcessMockAgentRoleDefinition(
    string RoleKey,
    Guid PartyId,
    string AgentName,
    string RoleTitle,
    string Summary,
    string Instructions,
    AgentWorkloadKind Workload);

public sealed record ProcessMockAgentCatalogContext(
    Guid ProviderId,
    IReadOnlyDictionary<string, Guid> AgentIdsByRoleKey);
