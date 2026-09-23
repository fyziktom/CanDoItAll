namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class ProviderInitializationOptions {
    public const string SectionName = "AgentFramework:Providers";

    public bool SeedDefaults { get; set; } = true;
}
