using CanDoItAll.Processes.Application;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessRuntimeOperatorDiagnosticDetailsBuilderTests
{
    [Fact]
    public void Create_keeps_lifecycle_recovery_guidance_generic_while_extracting_driver_tool_identifiers()
    {
        var details = ProcessRuntimeOperatorDiagnosticDetailsBuilder.Create(
            "process.adapter.runtime_lifecycle_correlation_missing",
            "Current receipts include browser_navigate, custom_driver_observe, and workspace_runtime_stop but their lifecycle correlation is incomplete.");

        Assert.NotNull(details);
        Assert.Equal("runtime-lifecycle-gate", details.GateId);
        Assert.Contains("browser_navigate", details.ReceiptRuleIds);
        Assert.Contains("custom_driver_observe", details.ReceiptRuleIds);
        Assert.Contains("workspace_runtime_stop", details.ReceiptRuleIds);
        Assert.DoesNotContain("browser", details.NextAction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("current-execution lifecycle", details.NextAction, StringComparison.OrdinalIgnoreCase);
    }
}
