namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessDevelopmentSeedService
{
    private static IReadOnlyList<ProcessTemplateBaselineScenario> GetBaselineScenarios(ProcessTemplatePack pack)
    {
        return pack.BaselineScenarios;
    }
}
