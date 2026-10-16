using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureProjectDefinitionResult {
    internal static T Require<T>(Result<T> result) {
        if (result.IsSuccess) {
            return result.Value!;
        }

        var error = result.Errors[0];
        var status = error.Code switch {
            ProjectErrorCodes.NotFound => 404,
            ProjectErrorCodes.DefinitionConflict or ProjectErrorCodes.DefinitionTransactionConflict or ProjectErrorCodes.LifetimeChanged or
                ProjectErrorCodes.ExternalIdentityConflict or ProjectErrorCodes.ExternalIdentityImmutable => 409,
            _ => 400
        };
        throw ProjectStructureAgentException.CreateAgentVisible(status, error.Code, error.Message,
            canRetryWithCorrectedInput: true, diagnosticDetails: result.Errors,
            effectState: AgentToolEffectState.NotCommitted);
    }
}
