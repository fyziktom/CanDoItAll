using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Modules.Memory.Services;

public static class MemoryProviderCapabilityPolicy
{
    public static IReadOnlyList<MemoryProviderDriverKind> ExecutableDriverKinds { get; } =
    [
        MemoryProviderDriverKind.Http,
        MemoryProviderDriverKind.NativeRemote,
        MemoryProviderDriverKind.Mcp,
        MemoryProviderDriverKind.Mock
    ];

    private static readonly IReadOnlySet<MemoryCapabilityId> HttpCapabilities = new HashSet<MemoryCapabilityId>
    {
        MemoryCapabilityIds.ContextQuerySync,
        MemoryCapabilityIds.UiRcl,
        MemoryCapabilityIds.UiIframe
    };

    private static readonly IReadOnlySet<MemoryCapabilityId> McpCapabilities = new HashSet<MemoryCapabilityId>
    {
        MemoryCapabilityIds.ContextQuerySync,
        MemoryCapabilityIds.ContextQueryAsync,
        MemoryCapabilityIds.OperationStatus,
        MemoryCapabilityIds.UiRcl,
        MemoryCapabilityIds.UiIframe
    };

    private static readonly IReadOnlySet<MemoryCapabilityId> MockCapabilities = new HashSet<MemoryCapabilityId>
    {
        MemoryCapabilityIds.ContextQuerySync,
        MemoryCapabilityIds.UiRcl,
        MemoryCapabilityIds.UiIframe
    };

    public static IReadOnlyList<MemoryCapabilityId> GetUnsupportedEnabledClaims(
        MemoryProviderProfileEditorModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var allowed = editor.DriverKind switch
        {
            MemoryProviderDriverKind.Http or MemoryProviderDriverKind.NativeRemote => HttpCapabilities,
            MemoryProviderDriverKind.Mcp => McpCapabilities,
            MemoryProviderDriverKind.Mock => MockCapabilities,
            _ => null
        };
        if (allowed is null)
        {
            return [];
        }

        return EnumerateEnabledClaims(editor)
            .Where(capability => !allowed.Contains(capability))
            .ToArray();
    }

    public static bool CanExecute(MemoryProviderDriverKind driverKind, MemoryCapabilityId capability) =>
        driverKind switch
        {
            MemoryProviderDriverKind.Http or MemoryProviderDriverKind.NativeRemote => HttpCapabilities.Contains(capability),
            MemoryProviderDriverKind.Mcp => McpCapabilities.Contains(capability),
            MemoryProviderDriverKind.Mock => MockCapabilities.Contains(capability),
            _ => false
        };

    public static bool CanAdvertise(MemoryProviderDriverKind driverKind, MemoryCapabilityId capability) =>
        CanExecute(driverKind, capability);

    public static bool CanCancelOperation(MemoryProviderDriverKind driverKind) => false;

    public static void Validate(MemoryProviderProfileEditorModel editor)
    {
        if (!ExecutableDriverKinds.Contains(editor.DriverKind))
        {
            throw new InvalidOperationException(
                $"Memory provider driver '{editor.DriverKind}' is not an executable production choice.");
        }

        var unsupported = GetUnsupportedEnabledClaims(editor);
        if (unsupported.Count > 0)
        {
            throw new InvalidOperationException(
                $"{editor.DriverKind} driver cannot execute advertised capability claim(s): " +
                string.Join(", ", unsupported.Select(capability => capability.Value)) +
                ". Clear those claims or choose a driver with matching runtime ports.");
        }

        if (editor.DriverKind != MemoryProviderDriverKind.Mcp)
        {
            return;
        }

        if ((editor.SupportsContextQuerySync || editor.SupportsContextQueryAsync) &&
            string.IsNullOrWhiteSpace(editor.Mcp.ContextQueryTool))
        {
            throw new InvalidOperationException("MCP query capability requires a configured context-query tool.");
        }

        if ((editor.SupportsContextQueryAsync || editor.SupportsOperationStatus) &&
            string.IsNullOrWhiteSpace(editor.Mcp.OperationStatusTool))
        {
            throw new InvalidOperationException("MCP asynchronous query and operation-status capability require an operation-status tool.");
        }

    }

    private static IEnumerable<MemoryCapabilityId> EnumerateEnabledClaims(MemoryProviderProfileEditorModel editor)
    {
        if (editor.SupportsContextQuerySync) yield return MemoryCapabilityIds.ContextQuerySync;
        if (editor.SupportsContextQueryAsync) yield return MemoryCapabilityIds.ContextQueryAsync;
        if (editor.SupportsSnapshotIngestion) yield return MemoryCapabilityIds.IngestionSnapshot;
        if (editor.SupportsProviderRequestedSources) yield return MemoryCapabilityIds.IngestionProviderRequestedSource;
        if (editor.SupportsImmediateFeedback) yield return MemoryCapabilityIds.FeedbackImmediate;
        if (editor.SupportsDelayedFeedback) yield return MemoryCapabilityIds.FeedbackDelayed;
        if (editor.SupportsProviderEvents) yield return MemoryCapabilityIds.EventsProviderPush;
        if (editor.SupportsHostEventPolling) yield return MemoryCapabilityIds.EventsHostPoll;
        if (editor.SupportsOperationStatus) yield return MemoryCapabilityIds.OperationStatus;
        if (editor.SupportsRclUi) yield return MemoryCapabilityIds.UiRcl;
        if (editor.SupportsIframeUi) yield return MemoryCapabilityIds.UiIframe;
    }
}
