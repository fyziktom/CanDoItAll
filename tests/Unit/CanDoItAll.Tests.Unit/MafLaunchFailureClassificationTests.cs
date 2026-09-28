using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafLaunchFailureClassificationTests
{
    private const string StartFailureMessage =
        "Recipe 'powershell_run_script' could not start: The program this command needs was not found on the host (pwsh or powershell). [ProcessStartFailed.ExecutableNotFound]";

    [Theory]
    [InlineData("ProcessStartFailed.ExecutableNotFound", false)]
    [InlineData("ProcessStartFailed.WorkingDirectoryMissing", true)]
    [InlineData("ProcessStartFailed.WorkingDirectoryTooLong", false)]
    public void A_workspace_start_failure_carries_its_code_and_retry_flag_into_the_trace(string code, bool canRetry)
    {
        var result = JsonSerializer.SerializeToElement(new { succeeded = false, message = StartFailureMessage, failureCode = code });

        var assessment = MafRuntimeToolInvocationResultClassifier.Assess(
            ToolContractCatalog.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            result,
            rejectedBeforeEffect: true);

        Assert.Equal(AgentToolInvocationOutcome.Failed, assessment.Outcome);
        Assert.Equal(AgentToolEffectState.NotCommitted, assessment.EffectState);
        Assert.Equal(code, assessment.FailureCode);
        Assert.Equal(canRetry, assessment.CanRetryWithCorrectedInput);
        Assert.Equal(StartFailureMessage, assessment.FailureMessage);
    }

    [Fact]
    public void A_start_failure_without_the_no_effect_record_stays_unknown()
    {
        var result = JsonSerializer.SerializeToElement(new
        {
            succeeded = false,
            message = StartFailureMessage,
            failureCode = "ProcessStartFailed.ProcessOwnershipFailed"
        });

        var assessment = MafRuntimeToolInvocationResultClassifier.Assess(
            ToolContractCatalog.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            result);

        Assert.Equal(AgentToolEffectState.Unknown, assessment.EffectState);
        Assert.Equal("ProcessStartFailed.ProcessOwnershipFailed", assessment.FailureCode);
    }

    [Fact]
    public void An_untrusted_tool_cannot_claim_a_launch_code_or_retry_flag()
    {
        var result = JsonSerializer.SerializeToElement(new
        {
            succeeded = false,
            message = "failed",
            failureCode = "ProcessStartFailed.WorkingDirectoryMissing"
        });

        var assessment = MafRuntimeToolInvocationResultClassifier.Assess(
            "external_mcp_tool",
            ToolInvocationClassification.Mutation,
            result,
            rejectedBeforeEffect: true);

        Assert.Empty(assessment.FailureCode);
        Assert.False(assessment.CanRetryWithCorrectedInput);
    }

    [Fact]
    public void A_failed_tool_result_becomes_a_short_stream_line_with_the_reason_before_the_code()
    {
        var result = JsonSerializer.SerializeToElement(new
        {
            succeeded = false,
            message = StartFailureMessage + " Additional guidance that the stream line leaves out.",
            failureCode = "ProcessStartFailed.ExecutableNotFound"
        });

        var line = MafToolFailureProgress.Describe(ToolContractCatalog.WorkspacePowerShellRunScript, result);

        Assert.Equal(
            "workspace_pwsh_run_script: Recipe 'powershell_run_script' could not start: The program this command needs was not found on the host (pwsh or powershell). [ProcessStartFailed.ExecutableNotFound]",
            line);
    }

    [Fact]
    public void Successful_or_unresolved_results_produce_no_stream_line()
    {
        Assert.Null(MafToolFailureProgress.Describe(
            ToolContractCatalog.WorkspacePowerShellRunScript,
            JsonSerializer.SerializeToElement(new { succeeded = true, message = "done" })));
        Assert.Null(MafToolFailureProgress.Describe(
            ToolContractCatalog.WorkspacePowerShellRunScript,
            JsonSerializer.SerializeToElement(new { value = 42 })));
        Assert.Null(MafToolFailureProgress.Describe(ToolContractCatalog.WorkspacePowerShellRunScript, null));
    }

    [Fact]
    public void A_failure_without_a_code_names_the_tool_and_the_first_sentence()
    {
        var line = MafToolFailureProgress.Describe(
            null,
            JsonSerializer.SerializeToElement(new
            {
                succeeded = false,
                message = "Recipe 'dotnet_build' failed with exit code 1. Inspect captured diagnostics before editing or retrying."
            }));

        Assert.Equal("A tool: Recipe 'dotnet_build' failed with exit code 1.", line);
    }
}
