using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed partial class ProcessManagerControlLoop(ProcessManagerRuntimeDependencies runtimeDependencies)
{
    private static readonly ProcessEventActor ManagerActor = new(
        ProcessEventActorKind.Manager,
        new ProcessActorId("process-manager"));

    private readonly ProcessManagerRuntimeDependencies dependencies = runtimeDependencies;
}
