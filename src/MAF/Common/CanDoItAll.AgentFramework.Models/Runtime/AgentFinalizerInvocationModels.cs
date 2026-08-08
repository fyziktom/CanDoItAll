// Pure evidence data moved from CanDoItAll.AgentFramework.Core (SB09).
// The namespace is intentionally kept as CanDoItAll.AgentFramework.Core to preserve
// serialization identity and avoid using-churn in existing consumers.
namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentFinalizerInvocation(
    string ToolName,
    string ArgumentsJson,
    int Sequence);
