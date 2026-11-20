using System.ComponentModel;

namespace CanDoItAll.AgentFramework.Models;

public static class RemoteAgentTemplateRoutes {
    public const string Catalog = "api/shared-agent-templates/v1/templates";

    public static string Details(Guid id) => $"{Catalog}/{id:D}";
}

[Description("One selectable agent template in the publisher workspace.")]
public sealed record RemoteAgentTemplateSummary(
    [property: Description("Publisher-local template ID for the template detail route.")] Guid Id,
    [property: Description("Display name of the template.")] string Name,
    [property: Description("Short description of the agent's purpose.")] string Summary,
    [property: Description("Revision timestamp of the agent definition.")] DateTimeOffset UpdatedAtUtc,
    [property: Description("Number of assigned skills, tools and other capabilities.")] int CapabilityCount) {
    [Description("Public identity of the publishing instance.")] public Guid PublisherId { get; init; }
    [Description("Publisher catalog revision at selection, including capability changes.")] public long CatalogRevision { get; init; }
}

[Description("Version-one portable definition for creating an independent local draft agent.")]
public sealed record RemoteAgentTemplate(
    [property: Description("Publisher-local template ID. The local working copy receives a fresh ID.")] Guid Id,
    [property: Description("Display name of the agent to create.")] string Name,
    [property: Description("Role title shown in the agent editor.")] string RoleTitle,
    [property: Description("Short description of the agent's purpose.")] string Summary,
    [property: Description("System instructions for the local agent.")] string Instructions,
    [property: Description("Workload category: 0 General, 1 Management, 2 Hr, 3 Sales, 4 Assistant, 5 Qa, 6 Support, 7 Programming, 8 Spreadsheet, 9 Mail, 10 Research.")] AgentWorkloadKind Workload,
    [property: Description("Searchable labels assigned to the definition.")] IReadOnlyList<string> Tags,
    [property: Description("Capability declarations required by this template.")] IReadOnlyList<RemoteAgentTemplateCapability> Capabilities,
    [property: Description("Revision timestamp of the agent definition shown in the catalog.")] DateTimeOffset UpdatedAtUtc) {
    [Description("Public identity of the publishing instance.")] public Guid PublisherId { get; init; }
    [Description("Publisher catalog revision from which the content and all dependencies were read.")] public long CatalogRevision { get; init; }
}

public sealed record RemoteAgentTemplateBinding(Guid SourceId, Guid PublisherId, Guid TemplateId, Guid AgentId);

[Description("An assigned runtime capability identified by its kind and catalog key.")]
public sealed record RemoteAgentTemplateCapability(
    [property: Description("Capability category: 0 McpServer, 1 Skill, 2 Tool, 3 Plugin, 4 Rag, 5 AiContext, 6 Memory (retired).")] CapabilityKind Kind,
    [property: Description("Stable normalized capability key used to resolve a local installation.")] string Key,
    [property: Description("Portable skill content when this declaration represents an inline skill. All other declarations require a matching local capability.")] RemoteAgentInlineSkill? InlineSkill);

[Description("Text and embedded resources of an inline skill.")]
public sealed record RemoteAgentInlineSkill(
    [property: Description("Lowercase hyphenated name exposed to the skill runtime.")] string Name,
    [property: Description("Purpose of the skill shown during discovery.")] string Description,
    [property: Description("Skill guidance supplied on activation.")] string Instructions,
    [property: Description("Named in-memory resources accompanying the instructions.")] IReadOnlyList<RemoteAgentSkillResource> Resources);

[Description("One named text resource bundled with an inline skill.")]
public sealed record RemoteAgentSkillResource(
    [property: Description("Name used to request the resource from the skill.")] string Name,
    [property: Description("Embedded text returned by the local skill runtime.")] string Content,
    [property: Description("Optional purpose or usage guidance for this resource.")] string? Description);
