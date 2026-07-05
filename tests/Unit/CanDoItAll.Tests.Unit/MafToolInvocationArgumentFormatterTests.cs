using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

public sealed class MafToolInvocationArgumentFormatterTests
{
    [Fact]
    public void DescribeToolInvocation_summarizes_arguments_without_multiline_payloads()
    {
        var longValue = new string('x', 130);
        var toolCall = new FunctionCallContent(
            "call-1",
            "workspace_write_file",
            new Dictionary<string, object?>
            {
                ["path"] = "artifacts/result.md",
                ["content"] = longValue
            });

        var description = MafToolInvocationArgumentFormatter.DescribeToolInvocation(toolCall);

        Assert.Contains("Invoking tool 'workspace_write_file'", description, StringComparison.Ordinal);
        Assert.Contains("path=\"artifacts/result.md\"", description, StringComparison.Ordinal);
        Assert.Contains($"content=\"{new string('x', 120)}...#{StableContentHash.ComputeShortSha256Hex(longValue)}\"", description, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeArguments_returns_empty_summary_for_invalid_json()
    {
        var summary = MafToolInvocationArgumentFormatter.DescribeArguments("not-json");

        Assert.Empty(summary);
    }
}
