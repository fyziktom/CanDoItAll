// Template: IHarmonicAssistantModule
// Keep comments in English.

public interface IHarmonicAssistantModule
{
    string Id { get; }
    bool IsEnabled { get; set; }
    ModuleContribution Evaluate(ModuleContext context);
}
