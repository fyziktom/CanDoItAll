using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafToolInvocationArgumentFormatterTests
{
    [Fact]
    public void DescribeToolInvocation_data_minimizes_content_while_retaining_target_path()
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
        Assert.DoesNotContain(longValue, description, StringComparison.Ordinal);
        Assert.Contains("redacted", description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DescribeArguments_returns_empty_summary_for_invalid_json()
    {
        var summary = MafToolInvocationArgumentFormatter.DescribeArguments("not-json");

        Assert.Empty(summary);
    }

    [Fact]
    public void DescribeToolInvocation_masks_top_level_and_nested_secrets_without_removing_project_targets()
    {
        const string leaseToken = "progress-lease-token-sentinel";
        const string nestedApiToken = "progress-nested-api-token-sentinel";
        const string topLevelApiToken = "progress-top-level-api-token-sentinel";
        var toolCall = new FunctionCallContent(
            "call-2",
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeUpdate,
            new Dictionary<string, object?>
            {
                ["apiToken"] = topLevelApiToken,
                ["request"] = new
                {
                    projectId = "project-42",
                    leaseToken,
                    node = new
                    {
                        nodeId = "node-7",
                        api_key = nestedApiToken
                    }
                }
            });

        var description = MafToolInvocationArgumentFormatter.DescribeToolInvocation(toolCall);

        Assert.DoesNotContain(leaseToken, description, StringComparison.Ordinal);
        Assert.DoesNotContain(nestedApiToken, description, StringComparison.Ordinal);
        Assert.DoesNotContain(topLevelApiToken, description, StringComparison.Ordinal);
        Assert.Contains("apiToken=\"<redacted>\"", description, StringComparison.Ordinal);
        Assert.Contains("project-42", description, StringComparison.Ordinal);
        Assert.Contains("node-7", description, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeToolInvocation_copy_nodes_retains_source_and_destination_ids_without_exposing_lease()
    {
        const string leaseToken = "copy-nodes-lease-token-sentinel";
        var toolCall = new FunctionCallContent(
            "call-copy-nodes",
            AgentToolInvocationPolicyMetadata.ProjectStructureNodesCopy,
            new Dictionary<string, object?>
            {
                ["projectId"] = "project-42",
                ["request"] = new
                {
                    sourceNodeIds = new[] { "source-node-1", "source-node-2" },
                    destinationParentNodeId = "destination-node-7",
                    leaseToken
                }
            });

        var description = MafToolInvocationArgumentFormatter.DescribeToolInvocation(toolCall);

        Assert.Contains("project-42", description, StringComparison.Ordinal);
        Assert.Contains("source-node-1", description, StringComparison.Ordinal);
        Assert.Contains("source-node-2", description, StringComparison.Ordinal);
        Assert.Contains("destination-node-7", description, StringComparison.Ordinal);
        Assert.DoesNotContain(leaseToken, description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AgentToolInvocationPolicyMetadata.HrAgentSettingsUpdate)]
    [InlineData(AgentToolInvocationPolicyMetadata.PromptGalleryDraftUpdate)]
    [InlineData(AgentToolInvocationPolicyMetadata.WorkflowCuratorDraftUpdate)]
    [InlineData(AgentToolInvocationPolicyMetadata.CapabilityCuratorSave)]
    public void SummarizeArguments_retains_business_text_masking_for_managed_tools(string toolName)
    {
        var summary = MafToolInvocationArgumentFormatter.SummarizeArguments(
            toolName,
            new Dictionary<string, object?>
            {
                ["request"] = new
                {
                    itemId = "item-42",
                    name = "private-name-sentinel",
                    prompt = "private-prompt-sentinel"
                }
            });

        Assert.DoesNotContain("private-name-sentinel", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("private-prompt-sentinel", summary, StringComparison.Ordinal);
        Assert.Contains("item-42", summary, StringComparison.Ordinal);
        Assert.Contains("\\u003Credacted\\u003E#", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void SummarizeArguments_fails_closed_when_an_argument_cannot_be_serialized()
    {
        const string secret = "unserializable-argument-secret";

        var summary = MafToolInvocationArgumentFormatter.SummarizeArguments(
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeUpdate,
            new Dictionary<string, object?>
            {
                ["request"] = new UnserializableArgument(secret)
            });

        Assert.DoesNotContain(secret, summary, StringComparison.Ordinal);
        Assert.Contains("<redacted>#", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_tool_progress_summary_data_minimizes_business_content()
    {
        const string title = "confidential-roadmap-title";
        const string content = "confidential-roadmap-content";

        var summary = MafToolInvocationArgumentFormatter.SummarizeArguments(
            "third_party_mutation",
            new Dictionary<string, object?>
            {
                ["request"] = new
                {
                    projectId = "project-42",
                    nodeId = "node-7",
                    title,
                    content
                }
            });

        Assert.Contains("project-42", summary, StringComparison.Ordinal);
        Assert.Contains("node-7", summary, StringComparison.Ordinal);
        Assert.DoesNotContain(title, summary, StringComparison.Ordinal);
        Assert.DoesNotContain(content, summary, StringComparison.Ordinal);
        Assert.Contains("redacted", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generic_tool_progress_summary_fails_closed_for_unknown_fields_and_arrays()
    {
        var sensitiveValues = new[]
        {
            "private-body",
            "private-message",
            "private-subject",
            "private-caption",
            "private-markdown",
            "private-array-a",
            "private-array-b",
            "private-nested-value"
        };
        var summary = MafToolInvocationArgumentFormatter.SummarizeArguments(
            "third_party_mutation",
            new Dictionary<string, object?>
            {
                ["request"] = new
                {
                    nodeId = "node-7",
                    body = sensitiveValues[0],
                    message = sensitiveValues[1],
                    subject = sensitiveValues[2],
                    caption = sensitiveValues[3],
                    markdown = sensitiveValues[4],
                    grid = "private-grid",
                    monkeys = "private-monkeys",
                    status = "private-status",
                    data = new[] { sensitiveValues[5], sensitiveValues[6] },
                    payload = new
                    {
                        value = sensitiveValues[7]
                    }
                }
            });

        Assert.Contains("node-7", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("private-grid", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("private-monkeys", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("private-status", summary, StringComparison.Ordinal);
        Assert.All(
            sensitiveValues,
            sensitiveValue => Assert.DoesNotContain(
                sensitiveValue,
                summary,
                StringComparison.Ordinal));
        Assert.Contains("redacted", summary, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class UnserializableArgument(string secret)
    {
        public Action UnsupportedValue { get; } = static () => { };

        public override string ToString()
        {
            return secret;
        }
    }
}
