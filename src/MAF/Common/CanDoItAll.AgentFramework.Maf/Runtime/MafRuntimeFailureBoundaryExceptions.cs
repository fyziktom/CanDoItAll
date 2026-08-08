using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.AgentFramework.Maf;

internal enum MafProviderConfigurationFailureReason
{
    MissingCredential,
    InvalidEndpoint,
    InvalidModel,
    UnsupportedProviderKind,
    UnsupportedTransport,
    InvalidRuntimeSettings
}

internal sealed class MafProviderConfigurationException : Exception
{
    public MafProviderConfigurationException(
        ProviderProfile provider,
        string model,
        MafProviderConfigurationFailureReason reason,
        Exception? innerException = null)
        : base("Provider runtime configuration validation failed.", innerException)
    {
        ArgumentNullException.ThrowIfNull(provider);

        Reason = reason;
        ProviderIdentity = new AgentRuntimeProviderFailureIdentity(
            provider.Id,
            provider.Name,
            provider.Kind,
            provider.Transport,
            model);
    }

    public MafProviderConfigurationFailureReason Reason { get; }

    public AgentRuntimeProviderFailureIdentity ProviderIdentity { get; }
}

internal sealed class MafToolInvocationBoundaryException : Exception
{
    public MafToolInvocationBoundaryException(
        string toolName,
        Exception innerException)
        : base("An agent tool invocation failed.", innerException)
    {
        ToolName = toolName;
    }

    public string ToolName { get; }
}
