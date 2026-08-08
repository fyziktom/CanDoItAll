using System.Reflection;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using Microsoft.Agents.AI;

namespace CanDoItAll.AgentFramework.Maf;

/// <summary>
/// Native MAF implementation of <see cref="IAgentRuntimeStateAdapter"/>. Wraps the raw
/// session payload produced by <see cref="MafRuntimeSessionPersistenceDriver"/> into a
/// <see cref="RuntimeStateEnvelope"/> and unwraps an already-judged-compatible envelope back
/// to that raw payload. This adapter never decides compatibility itself — that is
/// <see cref="MafRuntimeStateCompatibilityPolicy"/>'s job — it only serializes/deserializes.
/// </summary>
internal sealed class MafRuntimeStateAdapter : IAgentRuntimeStateAdapter
{
    private static readonly Lazy<string> CachedAdapterPackageVersion = new(ResolveAdapterPackageVersion);

    /// <summary>The current Microsoft.Agents.AI package version this adapter runs against.</summary>
    internal static string AdapterPackageVersion => CachedAdapterPackageVersion.Value;

    public string AdapterId => RuntimeStateAdapterIds.Maf;

    public RuntimeStateEnvelope CreateEnvelope(AgentRuntimeStateCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new RuntimeStateEnvelope(
            AdapterId,
            RuntimeStateEnvelope.CurrentSchemaVersion,
            CachedAdapterPackageVersion.Value,
            request.ProviderProfileId,
            request.ProviderTransport,
            request.Model,
            request.ToolsetFingerprint,
            request.ContextPolicyFingerprint,
            request.CapturedAtUtc,
            request.PayloadJson)
        {
            HistoryMode = request.HistoryMode,
            AuthorityPolicyFingerprint = request.AuthorityPolicyFingerprint,
            CapabilityPolicyFingerprint = request.CapabilityPolicyFingerprint
        };
    }

    public AgentRuntimeStateRestoreResult TryRestore(RuntimeStateEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!envelope.IsCompatibleWith(AdapterId, minimumSchemaVersion: 1, RuntimeStateEnvelope.CurrentSchemaVersion))
        {
            return AgentRuntimeStateRestoreResult.Failed(
                $"Envelope adapter '{envelope.AdapterId}' schema {envelope.SchemaVersion} is not readable by adapter '{AdapterId}' (readable schema range 1-{RuntimeStateEnvelope.CurrentSchemaVersion}).");
        }

        if (string.IsNullOrWhiteSpace(envelope.PayloadJson))
        {
            return AgentRuntimeStateRestoreResult.Failed("Envelope payload is empty.");
        }

        return AgentRuntimeStateRestoreResult.Restored(envelope.PayloadJson);
    }

    /// <summary>
    /// Resolves the Microsoft.Agents.AI package's informational version through the loaded
    /// assembly (cached — reflection only runs once per process).
    /// </summary>
    private static string ResolveAdapterPackageVersion()
    {
        var assembly = typeof(AIAgent).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? string.Empty;
    }
}
