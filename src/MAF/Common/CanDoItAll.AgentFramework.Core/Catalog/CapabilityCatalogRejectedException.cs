namespace CanDoItAll.AgentFramework.Core;

/// <summary>
/// The capability catalog refused a save before it persisted anything: the edited capability was missing, the candidate
/// would reuse another capability's identity, or its expected fingerprint no longer matched the stored capability.
/// </summary>
public sealed class CapabilityCatalogRejectedException(string message, bool isConcurrencyConflict = false)
    : InvalidOperationException(message)
{
    public bool IsConcurrencyConflict { get; } = isConcurrencyConflict;
}
