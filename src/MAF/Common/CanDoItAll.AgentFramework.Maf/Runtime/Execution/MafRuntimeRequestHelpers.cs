using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

/// <summary>
/// Small static helpers genuinely shared by the execution and continuation adapters:
/// runtime tool provider session key resolution and provider configuration failure mapping.
/// </summary>
internal static class MafRuntimeRequestHelpers
{
    public static string ResolveRuntimeToolProviderSessionKey(
        ChatSessionRecord session,
        string? runtimeSessionKey)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!string.IsNullOrWhiteSpace(runtimeSessionKey))
        {
            return runtimeSessionKey.Trim();
        }

        if (!string.IsNullOrWhiteSpace(session.Compatibility?.RuntimeSessionKey))
        {
            return session.Compatibility.RuntimeSessionKey.Trim();
        }

        return session.Id == Guid.Empty
            ? string.Empty
            : session.Id.ToString("D");
    }

    public static AgentRuntimeUsageException CreateProviderConfigurationUsageException(
        MafProviderConfigurationException exception)
        => new(
            "Provider runtime configuration validation failed before dispatch.",
            exception,
            [],
            failureOrigin: AgentRuntimeFailureOrigin.ProviderConfiguration,
            providerFailureIdentity: exception.ProviderIdentity);
}
