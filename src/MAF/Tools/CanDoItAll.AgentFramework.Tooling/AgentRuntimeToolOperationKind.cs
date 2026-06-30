namespace CanDoItAll.AgentFramework.Tooling;

public enum AgentRuntimeToolOperationKind
{
    Unknown = 0,
    Read = 1,
    Mutation = 2,
    Validation = 3,
    HostedProviderNative = 4,
    LocalMcp = 5,
    HostedMcp = 6
}
