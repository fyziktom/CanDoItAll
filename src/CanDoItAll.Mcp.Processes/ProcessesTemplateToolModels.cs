using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Mcp.Processes;

public sealed class ProcessTemplateImportRequest
{
    public string ProcessKey { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    public string DefinitionName { get; set; } = string.Empty;

    public bool AutoPublish { get; set; } = true;
}

public sealed record ProcessTemplateDetailToolData(
    ProcessTemplateCatalogItem Summary,
    ProcessTemplateDefinition Template,
    string CompatibilityReportMarkdown,
    IReadOnlyList<string> SupportingFiles);
