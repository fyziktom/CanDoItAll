using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.Memory.Services;

namespace CanDoItAll.Modules.Memory.Components;

internal static class MemoryUiFormatting
{
    public static string LedgerTone(MemoryLedgerStatus status) => status switch
    {
        MemoryLedgerStatus.Completed => "success",
        MemoryLedgerStatus.Accepted or
            MemoryLedgerStatus.Pending or
            MemoryLedgerStatus.Running => "info",
        MemoryLedgerStatus.Cancelled or
            MemoryLedgerStatus.Forgotten => "neutral",
        MemoryLedgerStatus.Expired => "warning",
        _ => "danger"
    };

    public static string UiSurfaceTone(MemoryProviderUiSurfaceAvailability availability) => availability switch
    {
        MemoryProviderUiSurfaceAvailability.Available => "success",
        MemoryProviderUiSurfaceAvailability.InvalidUrl => "danger",
        MemoryProviderUiSurfaceAvailability.UnsupportedKind => "neutral",
        _ => "warning"
    };

    public static string ToTestIdSegment(string value)
    {
        var chars = value
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        return new string(chars);
    }
}

internal static class MemoryProviderUiSurfaceParameterNames
{
    public const string Provider = "Provider";
    public const string Surface = "Surface";
}
