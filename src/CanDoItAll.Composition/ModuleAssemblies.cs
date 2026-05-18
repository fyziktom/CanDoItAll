using System.Reflection;
using CanDoItAll.Modules.Activity;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.SchedulerPlanner;
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
        typeof(AgentFrameworkModuleAssemblyMarker).Assembly,
        typeof(AutomationModuleAssemblyMarker).Assembly,
        typeof(CognitiveMemoryModuleAssemblyMarker).Assembly,
        typeof(CollaborationModuleAssemblyMarker).Assembly,
        typeof(CrmHrModuleAssemblyMarker).Assembly,
        typeof(FactoryModuleAssemblyMarker).Assembly,
        typeof(PluginsModuleAssemblyMarker).Assembly,
        typeof(ProjectsModuleAssemblyMarker).Assembly,
        typeof(ProcessesModuleAssemblyMarker).Assembly,
        typeof(PromptsModuleAssemblyMarker).Assembly,
        typeof(ResourcesModuleAssemblyMarker).Assembly,
        typeof(SchedulerPlannerModuleAssemblyMarker).Assembly,
        typeof(SecurityModuleAssemblyMarker).Assembly,
        typeof(TestLabModuleAssemblyMarker).Assembly,
        typeof(ValidationModuleAssemblyMarker).Assembly,
        typeof(WorkbenchModuleAssemblyMarker).Assembly,
        typeof(WorkspaceModuleAssemblyMarker).Assembly
    ];
}
