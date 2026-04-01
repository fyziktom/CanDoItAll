using System.Reflection;
using CanDoItAll.Modules.Activity;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Validation;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;

namespace CanDoItAll.Composition;

public static class ModuleAssemblies
{
    public static readonly Assembly[] All =
    [
        typeof(ActivityModuleAssemblyMarker).Assembly,
        typeof(AutomationModuleAssemblyMarker).Assembly,
        typeof(FactoryModuleAssemblyMarker).Assembly,
        typeof(ProjectsModuleAssemblyMarker).Assembly,
        typeof(PromptsModuleAssemblyMarker).Assembly,
        typeof(ResourcesModuleAssemblyMarker).Assembly,
        typeof(SecurityModuleAssemblyMarker).Assembly,
        typeof(TestLabModuleAssemblyMarker).Assembly,
        typeof(ValidationModuleAssemblyMarker).Assembly,
        typeof(WorkbenchModuleAssemblyMarker).Assembly,
        typeof(WorkspaceModuleAssemblyMarker).Assembly
    ];
}
