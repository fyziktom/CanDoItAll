namespace CanDoItAll.Tests.Support;

[Flags]
public enum TestSchemaBootstrapModules
{
    None = 0,
    Workspace = 1 << 0,
    Projects = 1 << 1,
    Workbench = 1 << 2,
    ProjectStructureAgent = 1 << 3,
    Default = Workspace | Projects,
    Full = Workspace | Projects | Workbench | ProjectStructureAgent
}
