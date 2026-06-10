namespace CanDoItAll.Modules.Processes;

public enum ProcessTemplateInventoryFamily {
    SoftwareDevelopment = 1,
    BlazorDotNetApplication = 2,
    BusinessAnalysis = 3,
    MultiTeamDevelopment = 4
}

public enum ProcessTemplateInventoryResolutionKind {
    ExactTemplate = 1,
    MappedTemplate = 2
}

public sealed record ProcessTemplateCatalogInventoryItem(
    ProcessTemplateInventoryFamily Family,
    string TemplateKey,
    string RelativePath,
    ProcessTemplateInventoryResolutionKind ResolutionKind,
    string ResolutionSummary);

public static class ProcessTemplateCatalogInventory {
    public const string SoftwareDeliveryTemplateKey = "software-delivery";
    public const string BlazorAppDeliveryTemplateKey = "blazor-app-delivery";
    public const string BusinessPlanDevelopmentTemplateKey = "business-plan-development";

    public static IReadOnlyList<ProcessTemplateCatalogInventoryItem> RequiredRepresentativeTemplates { get; } =
    [
        new(
            ProcessTemplateInventoryFamily.SoftwareDevelopment,
            SoftwareDeliveryTemplateKey,
            "processes/software-delivery",
            ProcessTemplateInventoryResolutionKind.ExactTemplate,
            "Software-development template is the multi-step software delivery process."),
        new(
            ProcessTemplateInventoryFamily.BlazorDotNetApplication,
            BlazorAppDeliveryTemplateKey,
            "processes/blazor-app-delivery",
            ProcessTemplateInventoryResolutionKind.ExactTemplate,
            "Blazor/.NET application template is the Blazor app delivery process."),
        new(
            ProcessTemplateInventoryFamily.BusinessAnalysis,
            BusinessPlanDevelopmentTemplateKey,
            "processes/business-plan-development",
            ProcessTemplateInventoryResolutionKind.ExactTemplate,
            "Business-analysis template is the business plan development process."),
        new(
            ProcessTemplateInventoryFamily.MultiTeamDevelopment,
            SoftwareDeliveryTemplateKey,
            "processes/software-delivery",
            ProcessTemplateInventoryResolutionKind.MappedTemplate,
            "Multi-team development is represented by the multi-team software delivery and release governance template.")
    ];

    public static ProcessTemplateCatalogInventoryItem GetRequiredTemplate(ProcessTemplateInventoryFamily family) {
        return RequiredRepresentativeTemplates.Single(item => item.Family == family);
    }
}
