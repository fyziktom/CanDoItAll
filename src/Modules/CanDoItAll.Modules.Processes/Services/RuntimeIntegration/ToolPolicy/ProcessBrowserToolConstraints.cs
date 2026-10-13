using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessBrowserToolConstraints {
    private static readonly ToolIntegerInputConstraint BoxedDepth = new("depth", 2, Required: true, WhenTrue: "boxes");
    private static readonly ImmutableArray<ToolInputConstraint> Snapshot = [
        new ToolIntegerInputConstraint("depth", 4, Required: true),
        BoxedDepth
    ];
    private static readonly ImmutableArray<ToolInputConstraint> Screenshot = [
        new ToolBooleanInputConstraint("fullPage", AllowedValue: false)
    ];

    internal static string Rejection(ToolInputConstraint constraint) => constraint switch {
        ToolIntegerInputConstraint { WhenTrue: null } depth =>
            $"Governed process browser snapshots must set depth to {depth.Maximum} or less. Retry once with depth={BoxedDepth.Maximum} and do not repeat this blocked call.",
        ToolIntegerInputConstraint depth =>
            $"Governed process browser snapshots with element boxes must set depth to {depth.Maximum} or less because deeper boxed snapshots can produce oversized tool output. Retry once with depth={depth.Maximum} or boxes=false and do not repeat this blocked call.",
        ToolBooleanInputConstraint =>
            "Governed process browser screenshots must be viewport-bounded. Retry once with fullPage=false or omit fullPage, and do not repeat this blocked call.",
        _ => throw new NotSupportedException("Unsupported browser input constraint.")
    };

    internal static IReadOnlyList<ToolInputConstraint> For(string toolName) {
        if (toolName.Equals(ToolContractCatalog.BrowserSnapshot, StringComparison.OrdinalIgnoreCase)) {
            return Snapshot;
        }
        return toolName.Equals(ToolContractCatalog.BrowserTakeScreenshot, StringComparison.OrdinalIgnoreCase)
            ? Screenshot : [];
    }
}
