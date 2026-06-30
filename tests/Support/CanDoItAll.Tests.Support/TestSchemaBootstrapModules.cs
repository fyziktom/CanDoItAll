namespace CanDoItAll.Tests.Support;

[Flags]
public enum TestSchemaBootstrapModules
{
    None = 0,
    Workspace = 1 << 0,
    Projects = 1 << 1,
    PromptFactory = 1 << 2,
    Workbench = 1 << 3,
    ProjectStructureAgent = 1 << 4,
    Default = Workspace | Projects,
    Full = Workspace | Projects | PromptFactory | Workbench | ProjectStructureAgent
}
