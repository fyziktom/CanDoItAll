using System.Reflection;
using CanDoItAll.Memory.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Memory;
using CanDoItAll.Modules.LlmChats.Persistence;
using CanDoItAll.Modules.LlmChats.Ui;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Processes.Persistence;

namespace CanDoItAll.Composition;

public static class ModuleAssemblies
{
    public static readonly Assembly[] All =
    [
        typeof(AgentFrameworkModuleAssemblyMarker).Assembly,
        typeof(CollaborationModuleAssemblyMarker).Assembly,
        typeof(CrmHrModuleAssemblyMarker).Assembly,
        typeof(MemoryModuleAssemblyMarker).Assembly,
        typeof(PluginsModuleAssemblyMarker).Assembly,
        typeof(ProjectsModuleAssemblyMarker).Assembly,
        typeof(ProcessesModuleAssemblyMarker).Assembly,
        typeof(ProcessPersistenceAssemblyMarker).Assembly,
        typeof(MemoryPersistenceAssemblyMarker).Assembly,
        typeof(LlmChatsPersistenceAssemblyMarker).Assembly,
        typeof(LlmChatsUiAssemblyMarker).Assembly,
        typeof(PromptsModuleAssemblyMarker).Assembly,
        typeof(ResourcesModuleAssemblyMarker).Assembly,
        typeof(SchedulerPlannerModuleAssemblyMarker).Assembly,
        typeof(SecurityModuleAssemblyMarker).Assembly,
        typeof(TestLabModuleAssemblyMarker).Assembly,
        typeof(WorkbenchModuleAssemblyMarker).Assembly,
        typeof(WorkspaceModuleAssemblyMarker).Assembly
    ];
}
