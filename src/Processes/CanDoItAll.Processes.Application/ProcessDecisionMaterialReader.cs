using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public interface IProcessDecisionMaterialReader {
    Task<ProcessDecisionMaterial?> ReadAsync(ProcessDecisionMaterialScope originalScope, ProcessDecisionMaterialReference reference,
        CancellationToken cancellationToken = default);
}
