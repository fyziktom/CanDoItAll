using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureEditConflictException()
    : ProjectStructureAgentException(409, "ProjectStructureConcurrentMutation",
        "The original node changed or its expected state is missing. Read the current node and reconcile your changes before saving.",
        details: null, isSafeToExpose: true, canRetryWithCorrectedInput: true,
        effectState: AgentToolEffectState.NotCommitted);
