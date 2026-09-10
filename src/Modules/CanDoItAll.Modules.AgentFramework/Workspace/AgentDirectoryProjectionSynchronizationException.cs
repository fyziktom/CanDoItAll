using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentDirectoryProjectionSynchronizationException(
    Guid agentId,
    Exception innerException,
    AgentPackageImportReceipt? importReceipt = null,
    AgentExternalProvisioningReceipt? provisioningReceipt = null,
    Guid? partyId = null)
    : AiTechnicalAgentCommittedSaveException(agentId, partyId, innerException) {
    public Guid AgentId => TechnicalAgentId;
    public AgentPackageImportReceipt? ImportReceipt { get; } = importReceipt;
    public AgentExternalProvisioningReceipt? ProvisioningReceipt { get; } = provisioningReceipt;
}
