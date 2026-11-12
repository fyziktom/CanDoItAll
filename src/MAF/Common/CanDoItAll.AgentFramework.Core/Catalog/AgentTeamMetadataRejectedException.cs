namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentTeamMetadataRejectedException(string message) : InvalidOperationException(message);
