namespace CanDoItAll.AgentFramework.Models;

public interface IAgentToolOwnerObservationEvidence {
    bool RequiresOwnerReconciliation { get; }
}

public interface IAgentToolOwnerResultEvidence {
    AgentToolProtocolEnvelope OwnerResult { get; }
}
