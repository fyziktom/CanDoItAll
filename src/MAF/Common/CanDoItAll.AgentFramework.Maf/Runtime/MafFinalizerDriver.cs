using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafFinalizerDriver
{
    private const int MaxFinalizerRepairPreviousAssistantTextCharacters = 12_000;

    public static bool ShouldRequestMissingRequiredFinalizerRepair(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        AgentRuntimeExecutionOptions runtimeOptions,
        IReadOnlyCollection<ToolApprovalRequestContent> approvalRequests,
        IReadOnlyList<AgentFinalizerInvocation> finalizerInvocations,
        out AgentFinalizerPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        ArgumentNullException.ThrowIfNull(approvalRequests);
        ArgumentNullException.ThrowIfNull(finalizerInvocations);

        policy = AgentFinalizerPolicy.NotRequired;
        if (finalizerMode != AgentFinalizerMode.Required ||
            runtimeOptions.MaxStructuredOutputRepairAttempts <= 0 ||
            approvalRequests.Count > 0 ||
            !AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out policy))
        {
            return false;
        }

        var toolName = policy.ToolName;
        return !finalizerInvocations.Any(invocation =>
            string.Equals(invocation.ToolName, toolName, StringComparison.OrdinalIgnoreCase));
    }

    public static AITool ResolveRequiredFinalizerTool(
        AgentFinalizerPolicy policy,
        IReadOnlyList<AITool> finalizerTools)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(finalizerTools);

        var matchingFinalizerTools = finalizerTools
            .Where(tool => string.Equals(tool.Name, policy.ToolName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matchingFinalizerTools.Count != 1)
        {
            throw new InvalidOperationException(
                $"Cannot repair missing required finalizer '{policy.ToolName}' because the runtime did not expose exactly one matching finalizer tool.");
        }

        return matchingFinalizerTools[0];
    }

    public static void ConfigureRequiredFinalizerRepairChatOptions(
        ChatOptions chatOptions,
        AgentFinalizerPolicy policy,
        AITool finalizerTool)
    {
        ArgumentNullException.ThrowIfNull(chatOptions);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(finalizerTool);

        chatOptions.AllowMultipleToolCalls = false;
        chatOptions.Instructions = BuildRequiredFinalizerRepairInstructions(policy);
        chatOptions.Tools = [finalizerTool];
        chatOptions.ToolMode = ChatToolMode.RequireSpecific(policy.ToolName);
    }

    public static ChatClientAgentRunOptions CreateRequiredFinalizerRepairRunOptions(
        AgentFinalizerPolicy policy,
        AITool finalizerTool)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(finalizerTool);

        var chatOptions = new ChatOptions
        {
            AllowMultipleToolCalls = false,
            Instructions = BuildRequiredFinalizerRepairInstructions(policy),
            Tools = [finalizerTool],
            ToolMode = ChatToolMode.RequireSpecific(policy.ToolName)
        };

        return new ChatClientAgentRunOptions(chatOptions)
        {
            AllowBackgroundResponses = false,
            ContinuationToken = null
        };
    }

    public static string BuildRequiredFinalizerRepairInstructions(
        AgentFinalizerPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return
            $"You are completing a bounded finalizer repair turn for `{policy.OutputContract.ContractKey}`." + Environment.NewLine +
            $"Call `{policy.ToolName}` exactly once." + Environment.NewLine +
            "Do not call any other tool. Do not emit Markdown, prose, code fences, or machine JSON outside the finalizer tool call." + Environment.NewLine +
            BuildRequiredFinalizerArgumentInstructions(policy);
    }

    public static string BuildRequiredFinalizerJsonRepairInstructions(
        AgentFinalizerPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return
            $"You are completing a bounded typed-output repair turn for `{policy.OutputContract.ContractKey}`." + Environment.NewLine +
            "Return exactly one JSON object matching the requested contract. Do not use Markdown, prose, code fences, or tool calls." + Environment.NewLine +
            BuildRequiredFinalizerArgumentInstructions(policy);
    }

    public static ChatClientAgentRunOptions CreateRequiredFinalizerJsonRepairRunOptions()
    {
        return new ChatClientAgentRunOptions(new ChatOptions
        {
            AllowMultipleToolCalls = false,
            ToolMode = null,
            Tools = []
        })
        {
            AllowBackgroundResponses = false,
            ContinuationToken = null
        };
    }

    public static ChatMessage CreateRequiredFinalizerJsonRepairMessage(
        AgentFinalizerPolicy policy,
        AgentResponse previousResponse)
        => CreateRequiredFinalizerJsonRepairMessage(policy, previousResponse, string.Empty);

    public static ChatMessage CreateRequiredFinalizerJsonRepairMessage(
        AgentFinalizerPolicy policy,
        AgentResponse previousResponse,
        string repairContext)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(previousResponse);

        return new ChatMessage(
            ChatRole.User,
            BuildRequiredFinalizerJsonRepairPrompt(policy, previousResponse.Text, repairContext));
    }

    public static string BuildRequiredFinalizerJsonRepairPrompt(
        AgentFinalizerPolicy policy,
        string? previousAssistantText)
        => BuildRequiredFinalizerJsonRepairPrompt(policy, previousAssistantText, string.Empty);

    public static string BuildRequiredFinalizerJsonRepairPrompt(
        AgentFinalizerPolicy policy,
        string? previousAssistantText,
        string? repairContext)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var previousTextSummary = BuildBoundedFinalizerRepairPreviousTextSummary(previousAssistantText);
        var repairContextSummary = BuildBoundedFinalizerRepairContextSummary(repairContext);

        return
            $"The previous repair turn could not submit `{policy.ToolName}` through provider tool calling." + Environment.NewLine +
            $"Return exactly one JSON object for `{policy.OutputContract.ContractKey}` now." + Environment.NewLine +
            "Use only the prior response text, session context, tool results, and process artifacts already available in the conversation. If evidence is insufficient, return the contract's blocking or failure state with actionable next actions where the contract supports them." + Environment.NewLine +
            "Do not return a generic no-prior-evidence blocker when the repair context below lists current-run tool calls, observed artifact refs, or primary managed output refs. If completion is impossible because a required managed output was not written, name that missing primary write ref and the next tool action that must create it." + Environment.NewLine +
            previousTextSummary + Environment.NewLine +
            repairContextSummary;
    }

    public static ChatMessage CreateRequiredFinalizerRepairMessage(
        AgentFinalizerPolicy policy,
        AgentResponse previousResponse)
        => CreateRequiredFinalizerRepairMessage(policy, previousResponse, string.Empty);

    public static ChatMessage CreateRequiredFinalizerRepairMessage(
        AgentFinalizerPolicy policy,
        AgentResponse previousResponse,
        string repairContext)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(previousResponse);

        return new ChatMessage(
            ChatRole.User,
            BuildRequiredFinalizerRepairPrompt(policy, previousResponse.Text, repairContext));
    }

    public static string BuildRequiredFinalizerRepairPrompt(
        AgentFinalizerPolicy policy,
        string? previousAssistantText)
        => BuildRequiredFinalizerRepairPrompt(policy, previousAssistantText, string.Empty);

    public static string BuildRequiredFinalizerRepairPrompt(
        AgentFinalizerPolicy policy,
        string? previousAssistantText,
        string? repairContext)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var previousTextSummary = BuildBoundedFinalizerRepairPreviousTextSummary(previousAssistantText);
        var repairContextSummary = BuildBoundedFinalizerRepairContextSummary(repairContext);

        return
            $"The previous turn ended without the required `{policy.ToolName}` finalizer tool call.{Environment.NewLine}" +
            $"Call `{policy.ToolName}` exactly once now to submit the final governed `{policy.OutputContract.ContractKey}` outcome.{Environment.NewLine}" +
            "Use only the current session context, prior tool results, and process artifacts. If the available evidence is insufficient for a successful outcome, submit the contract's failure or blocking state with actionable next actions where the contract supports them." + Environment.NewLine +
            "Do not submit a generic no-prior-evidence blocker when the repair context below lists current-run tool calls, observed artifact refs, or primary managed output refs. If completion is impossible because a required managed output was not written, name that missing primary write ref and the next tool action that must create it." + Environment.NewLine +
            "Do not call any other tool. Do not emit Markdown, prose, or machine JSON outside the finalizer tool call." + Environment.NewLine +
            previousTextSummary + Environment.NewLine +
            repairContextSummary;
    }

    public static string BuildBoundedFinalizerRepairPreviousTextSummary(string? previousAssistantText)
    {
        if (string.IsNullOrWhiteSpace(previousAssistantText))
        {
            return "The previous turn returned no assistant text.";
        }

        var trimmed = previousAssistantText.Trim();
        if (trimmed.Length <= MaxFinalizerRepairPreviousAssistantTextCharacters)
        {
            return $"Previous assistant text:{Environment.NewLine}{trimmed}";
        }

        var headLength = MaxFinalizerRepairPreviousAssistantTextCharacters / 2;
        var tailLength = MaxFinalizerRepairPreviousAssistantTextCharacters - headLength;
        var head = trimmed[..headLength];
        var tail = trimmed[^tailLength..];
        return
            $"Previous assistant text (truncated from {trimmed.Length} to {MaxFinalizerRepairPreviousAssistantTextCharacters} characters for bounded finalizer repair):" + Environment.NewLine +
            head + Environment.NewLine +
            Environment.NewLine +
            "[... middle of previous assistant text omitted for bounded finalizer repair ...]" + Environment.NewLine +
            Environment.NewLine +
            tail;
    }

    public static string BuildRequiredFinalizerRepairContext(
        AgentResponse previousResponse,
        IReadOnlyList<AgentToolInvocationTrace> toolInvocationTraces,
        IEnumerable<ChatMessage> originalInputMessages)
    {
        ArgumentNullException.ThrowIfNull(previousResponse);
        ArgumentNullException.ThrowIfNull(toolInvocationTraces);
        ArgumentNullException.ThrowIfNull(originalInputMessages);

        var builder = new StringBuilder();
        var toolCallSummaries = BuildPreviousTurnToolCallSummaries(previousResponse);
        if (toolCallSummaries.Count > 0)
        {
            builder.AppendLine("Previous turn tool calls observed by the provider:");
            foreach (var summary in toolCallSummaries)
            {
                builder.AppendLine($"- {summary}");
            }
        }

        if (toolInvocationTraces.Count > 0)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine("Previous turn tool trace results:");
            foreach (var trace in toolInvocationTraces.OrderBy(item => item.Sequence).Take(20))
            {
                var status = trace.CompletedAtUtc is null
                    ? "started"
                    : trace.Succeeded
                        ? "succeeded"
                        : $"failed: {WorkflowExecutorRedaction.RedactText(trace.FailureMessage)}";
                builder.AppendLine($"- #{trace.Sequence} {trace.ToolName}: {status}");
            }
        }

        var inputSummary = BuildRequiredFinalizerRepairInputSummary(originalInputMessages);
        if (!string.IsNullOrWhiteSpace(inputSummary))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine("Original governed process brief lines relevant to finalization:");
            builder.Append(inputSummary);
        }

        return builder.ToString().Trim();
    }

    public static string BuildRequiredFinalizerArgumentInstructions(AgentFinalizerPolicy finalizerPolicy)
    {
        if (!string.Equals(
                finalizerPolicy.OutputContract.ContractKey,
                AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return "- Pass exactly one `result` object argument to `submit_process_step_outcome`; do not pass scalar `result`, `status`, `reason`, or `evidenceRefs` as sibling arguments." + Environment.NewLine +
               "- The `result` object must include `status`, `reason`, `branchOutcomeKey`, `branchOutcomeTitle`, `evidenceRefs`, `nextActions`, and `humanReadableSummaryMarkdown`. Use `Completed`, `Blocked`, `Failed`, `WaitingApproval`, or `Refused` for `status`." + Environment.NewLine +
               "- Do not copy placeholder evidence values. Evidence refs must be exact current-run refs already created or observed during this turn." + Environment.NewLine +
               "- If `status` is `Completed`, `evidenceRefs` must contain at least one concrete current-run evidence reference. If no such evidence exists, return `Blocked` or `Failed` with a concrete `nextActions` entry instead of claiming completion." + Environment.NewLine;
    }

    public static bool TryNormalizeFinalizerJsonRepairText(
        AgentFinalizerPolicy policy,
        string? repairText,
        out string argumentsJson,
        out string failureMessage)
    {
        ArgumentNullException.ThrowIfNull(policy);

        argumentsJson = string.Empty;
        failureMessage = string.Empty;
        if (!TryExtractJsonObject(repairText, out var rawJson))
        {
            failureMessage = "No JSON object was found in the repair response.";
            return false;
        }

        if (TryNormalizeKnownFinalizerOutput(policy, rawJson, out argumentsJson, out failureMessage))
        {
            return true;
        }

        if (TryDeserializeFinalizerOutput(policy, rawJson, out argumentsJson, out failureMessage))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("result", out var resultElement) &&
                resultElement.ValueKind == JsonValueKind.Object)
            {
                var resultRawJson = resultElement.GetRawText();
                if (TryNormalizeKnownFinalizerOutput(policy, resultRawJson, out argumentsJson, out failureMessage))
                {
                    return true;
                }

                if (TryDeserializeFinalizerOutput(policy, resultRawJson, out argumentsJson, out failureMessage))
                {
                    return true;
                }
            }
        }
        catch (JsonException exception)
        {
            failureMessage = exception.Message;
        }

        return false;
    }

    public static IReadOnlyList<AgentFinalizerInvocation> CreateEffectiveFinalizerInvocations(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        IReadOnlyList<AgentFinalizerInvocation> capturedInvocations,
        IReadOnlyList<AgentToolInvocationTrace> capturedToolInvocationTraces,
        IReadOnlyList<AgentFinalizerInvocation> streamedInvocations,
        IReadOnlyList<AgentFinalizerInvocation> synthesizedInvocations)
    {
        if (finalizerMode != AgentFinalizerMode.Required ||
            !AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out var policy))
        {
            return synthesizedInvocations.Count == 0
                ? capturedInvocations
                : capturedInvocations
                    .Concat(synthesizedInvocations)
                    .OrderBy(invocation => invocation.Sequence)
                    .ToList();
        }

        var normalizedCapturedInvocations = AgentFinalizerInvocationNormalizer.NormalizeRequired(policy, capturedInvocations);
        if (IsValidFinalizerInvocationSet(policy, normalizedCapturedInvocations))
        {
            return normalizedCapturedInvocations;
        }

        if (TrySelectLastValidFinalizerInvocation(policy, capturedInvocations, out var capturedInvocation))
        {
            return [capturedInvocation];
        }

        var normalizedStreamedInvocations = AgentFinalizerInvocationNormalizer.NormalizeRequired(policy, streamedInvocations);
        if (IsValidFinalizerInvocationSet(policy, normalizedStreamedInvocations))
        {
            return normalizedStreamedInvocations;
        }

        if (TrySelectLastValidFinalizerInvocation(policy, streamedInvocations, out var streamedInvocation))
        {
            return [streamedInvocation];
        }

        var normalizedSynthesizedInvocations = AgentFinalizerInvocationNormalizer.NormalizeRequired(policy, synthesizedInvocations);
        if (IsValidFinalizerInvocationSet(policy, normalizedSynthesizedInvocations))
        {
            return normalizedSynthesizedInvocations;
        }

        if (TrySelectLastValidFinalizerInvocation(policy, synthesizedInvocations, out var synthesizedInvocation))
        {
            return [synthesizedInvocation];
        }

        if (synthesizedInvocations.Count > 0)
        {
            return synthesizedInvocations;
        }

        if (capturedInvocations.Count > 0)
        {
            return capturedInvocations;
        }

        return streamedInvocations;
    }

    public static IReadOnlyList<AgentToolInvocationTrace> CreateEffectiveToolInvocationTraces(
        IReadOnlyList<AgentToolInvocationTrace> capturedToolInvocationTraces,
        IReadOnlyList<AgentToolInvocationTrace> streamedToolInvocationTraces)
    {
        if (streamedToolInvocationTraces.Count == 0)
        {
            return capturedToolInvocationTraces;
        }

        var capturedToolNames = capturedToolInvocationTraces
            .Select(trace => trace.ToolName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingStreamedTraces = streamedToolInvocationTraces
            .Where(trace => !capturedToolNames.Contains(trace.ToolName))
            .ToList();
        if (missingStreamedTraces.Count == 0)
        {
            return capturedToolInvocationTraces;
        }

        return capturedToolInvocationTraces
            .Concat(missingStreamedTraces)
            .OrderBy(trace => trace.Sequence)
            .ToList();
    }

    private static IReadOnlyList<string> BuildPreviousTurnToolCallSummaries(AgentResponse previousResponse)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var summaries = new List<string>();
        foreach (var toolCall in previousResponse.Messages.SelectMany(message => message.Contents).OfType<ToolCallContent>())
        {
            var key = MafToolInvocationArgumentFormatter.ResolveToolCallKey(toolCall);
            if (!seen.Add(key))
            {
                continue;
            }

            summaries.Add(MafToolInvocationArgumentFormatter.DescribeToolInvocation(toolCall));
            if (summaries.Count >= 20)
            {
                break;
            }
        }

        return summaries;
    }

    private static string BuildRequiredFinalizerRepairInputSummary(IEnumerable<ChatMessage> originalInputMessages)
    {
        var lines = new List<string>();
        foreach (var text in originalInputMessages
                     .SelectMany(message => message.Contents)
                     .OfType<TextContent>()
                     .Select(content => content.Text)
                     .Where(text => !string.IsNullOrWhiteSpace(text)))
        {
            foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (!IsFinalizerRepairRelevantInputLine(line))
                {
                    continue;
                }

                lines.Add(line);
                if (lines.Count >= 120)
                {
                    return string.Join(Environment.NewLine, lines);
                }
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsFinalizerRepairRelevantInputLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        return line.StartsWith("Process:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Step key:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Step title:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Process run id:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Managed artifact root:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Allowed operations:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Operation target scope:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Required upstream artifact slots:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Produced artifact slots:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Artifact refs to inspect", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Expectation key rule:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Primary write ref:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Runtime rule:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Completion rule:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Validation:", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("workspace_write_file", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("workspace_read_file", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("submit_process_step_outcome", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("evidenceRefs", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildBoundedFinalizerRepairContextSummary(string? repairContext)
    {
        if (string.IsNullOrWhiteSpace(repairContext))
        {
            return "Repair context summary: no prior tool call or governed brief summary was available.";
        }

        var trimmed = repairContext.Trim();
        if (trimmed.Length <= MaxFinalizerRepairPreviousAssistantTextCharacters)
        {
            return $"Repair context summary:{Environment.NewLine}{trimmed}";
        }

        var headLength = MaxFinalizerRepairPreviousAssistantTextCharacters / 2;
        var tailLength = MaxFinalizerRepairPreviousAssistantTextCharacters - headLength;
        var head = trimmed[..headLength];
        var tail = trimmed[^tailLength..];
        return
            $"Repair context summary (truncated from {trimmed.Length} to {MaxFinalizerRepairPreviousAssistantTextCharacters} characters):" + Environment.NewLine +
            head + Environment.NewLine +
            Environment.NewLine +
            "[... middle of repair context omitted for bounded finalizer repair ...]" + Environment.NewLine +
            Environment.NewLine +
            tail;
    }

    private static bool TryDeserializeFinalizerOutput(
        AgentFinalizerPolicy policy,
        string rawJson,
        out string argumentsJson,
        out string failureMessage)
    {
        argumentsJson = string.Empty;
        failureMessage = string.Empty;

        try
        {
            var output = JsonSerializer.Deserialize(rawJson, policy.OutputType, AgentOutputJson.SerializerOptions);
            if (output is null)
            {
                failureMessage = "The JSON payload deserialized to null.";
                return false;
            }

            argumentsJson = JsonSerializer.Serialize(output, policy.OutputType, AgentOutputJson.SerializerOptions);
            return true;
        }
        catch (JsonException exception)
        {
            failureMessage = exception.Message;
            return false;
        }
    }

    private static bool TryNormalizeKnownFinalizerOutput(
        AgentFinalizerPolicy policy,
        string rawJson,
        out string argumentsJson,
        out string failureMessage)
    {
        argumentsJson = string.Empty;
        failureMessage = string.Empty;

        if (policy.OutputType == typeof(ProcessStepOutcomeResult))
        {
            return TryNormalizeProcessStepOutcomeResultJson(rawJson, out argumentsJson, out failureMessage);
        }

        return false;
    }

    private static bool TryNormalizeProcessStepOutcomeResultJson(
        string rawJson,
        out string argumentsJson,
        out string failureMessage)
    {
        argumentsJson = string.Empty;
        failureMessage = string.Empty;

        try
        {
            if (JsonNode.Parse(rawJson) is not JsonObject jsonObject)
            {
                failureMessage = "The JSON payload was not an object.";
                return false;
            }

            NormalizeStringArrayProperty(jsonObject, "evidenceRefs");
            NormalizeStringArrayProperty(jsonObject, "nextActions");
            NormalizeProcessStepOutcomeReason(jsonObject);

            var output = jsonObject.Deserialize<ProcessStepOutcomeResult>(AgentOutputJson.SerializerOptions);
            if (output is null)
            {
                failureMessage = "The normalized JSON payload deserialized to null.";
                return false;
            }

            argumentsJson = JsonSerializer.Serialize(output, AgentOutputJson.SerializerOptions);
            return true;
        }
        catch (JsonException exception)
        {
            failureMessage = exception.Message;
            return false;
        }
    }

    private static void NormalizeProcessStepOutcomeReason(JsonObject jsonObject)
    {
        if (TryReadNonEmptyStringProperty(jsonObject, "reason", out _))
        {
            return;
        }

        if (TryReadNonEmptyStringProperty(jsonObject, "humanReadableSummaryMarkdown", out var humanSummary) ||
            TryReadNonEmptyStringProperty(jsonObject, "branchOutcomeTitle", out humanSummary))
        {
            jsonObject["reason"] = humanSummary;
        }
    }

    private static bool TryReadNonEmptyStringProperty(
        JsonObject jsonObject,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node))
        {
            return false;
        }

        value = ConvertJsonNodeToString(node).Trim();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static void NormalizeStringArrayProperty(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var value) ||
            value is not JsonArray values)
        {
            return;
        }

        var normalizedValues = new JsonArray();
        foreach (var item in values)
        {
            var text = ConvertJsonNodeToString(item);
            if (!string.IsNullOrWhiteSpace(text))
            {
                normalizedValues.Add(text);
            }
        }

        jsonObject[propertyName] = normalizedValues;
    }

    private static string ConvertJsonNodeToString(JsonNode? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        if (node is JsonValue value)
        {
            return value.TryGetValue<string>(out var text)
                ? text
                : value.ToJsonString();
        }

        if (node is JsonObject jsonObject)
        {
            return string.Join(
                "; ",
                jsonObject.Select(property => $"{property.Key}: {ConvertJsonNodeToString(property.Value)}"));
        }

        if (node is JsonArray jsonArray)
        {
            return string.Join(", ", jsonArray.Select(ConvertJsonNodeToString));
        }

        return node.ToJsonString();
    }

    private static bool TryExtractJsonObject(
        string? text,
        out string rawJson)
    {
        rawJson = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = trimmed.IndexOf('\n');
            var fenceEnd = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstLineEnd >= 0 && fenceEnd > firstLineEnd)
            {
                trimmed = trimmed[(firstLineEnd + 1)..fenceEnd].Trim();
            }
        }

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            rawJson = trimmed;
            return true;
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return false;
        }

        rawJson = trimmed[start..(end + 1)].Trim();
        return true;
    }

    private static bool IsValidFinalizerInvocationSet(
        AgentFinalizerPolicy policy,
        IReadOnlyList<AgentFinalizerInvocation> invocations)
    {
        if (invocations.Count == 0)
        {
            return false;
        }

        var validation = new DefaultAgentFinalizerValidator().Validate(policy, invocations);
        return validation.Succeeded && validation.Output is not null;
    }

    private static bool TrySelectLastValidFinalizerInvocation(
        AgentFinalizerPolicy policy,
        IReadOnlyList<AgentFinalizerInvocation> invocations,
        out AgentFinalizerInvocation invocation)
    {
        invocation = default!;
        for (var index = invocations.Count - 1; index >= 0; index--)
        {
            var candidate = invocations[index];
            var validation = new DefaultAgentFinalizerValidator().Validate(policy, [candidate]);
            if (validation.Succeeded && validation.Output is not null)
            {
                invocation = candidate;
                return true;
            }
        }

        return false;
    }

    private static AgentFinalizerInvocation? TryCreateStreamedFinalizerInvocation(
        AgentFinalizerPolicy policy,
        ToolCallContent toolCall,
        int sequence)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(toolCall);

        if (!policy.IsRequired ||
            !string.Equals(MafToolInvocationArgumentFormatter.ResolveToolName(toolCall), policy.ToolName, StringComparison.OrdinalIgnoreCase) ||
            toolCall is not FunctionCallContent functionCall ||
            functionCall.Arguments is null)
        {
            return null;
        }

        var payload = functionCall.Arguments.Count == 1 &&
                      functionCall.Arguments.TryGetValue("result", out var result)
            ? result
            : functionCall.Arguments;
        var argumentsJson = SerializeStreamedFinalizerPayload(payload);
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return null;
        }

        return new AgentFinalizerInvocation(
            policy.ToolName,
            argumentsJson,
            sequence);
    }

    private static string SerializeStreamedFinalizerPayload(object? payload)
    {
        return payload switch
        {
            null => "null",
            JsonElement jsonElement => SerializeStreamedFinalizerJsonElement(jsonElement),
            string text when TryUseRawJsonObjectOrArray(text, out var rawJson) => rawJson,
            string text => JsonSerializer.Serialize(text, AgentOutputJson.SerializerOptions),
            _ => JsonSerializer.Serialize(payload, AgentOutputJson.SerializerOptions)
        };
    }

    private static string SerializeStreamedFinalizerJsonElement(JsonElement jsonElement)
    {
        if (jsonElement.ValueKind == JsonValueKind.String &&
            TryUseRawJsonObjectOrArray(jsonElement.GetString(), out var rawJson))
        {
            return rawJson;
        }

        return jsonElement.GetRawText();
    }

    private static bool TryUseRawJsonObjectOrArray(string? text, out string rawJson)
    {
        rawJson = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) ||
            (trimmed.StartsWith('[') && trimmed.EndsWith(']')))
        {
            rawJson = trimmed;
            return true;
        }

        return false;
    }

    public sealed class StreamedFinalizerInvocationRecorder(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode)
    {
        private readonly object gate = new();
        private readonly AgentFinalizerPolicy? policy = finalizerMode == AgentFinalizerMode.Required &&
                                                        AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out var resolvedPolicy)
            ? resolvedPolicy
            : null;
        private readonly List<AgentFinalizerInvocation> finalizerInvocations = [];
        private readonly List<AgentToolInvocationTrace> toolInvocationTraces = [];
        private int nextSequence;

        public void Record(ToolCallContent toolCall)
        {
            var sequence = Interlocked.Increment(ref nextSequence);
            if (policy is null)
            {
                return;
            }

            var invocation = TryCreateStreamedFinalizerInvocation(policy, toolCall, sequence);
            if (invocation is null)
            {
                return;
            }

            lock (gate)
            {
                finalizerInvocations.Add(invocation);
                toolInvocationTraces.Add(new AgentToolInvocationTrace(
                    policy.ToolName,
                    ToolInvocationClassification.Read,
                    sequence,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    Succeeded: true,
                    FailureMessage: "Captured from a streamed required-finalizer tool call."));
            }
        }

        public IReadOnlyList<AgentFinalizerInvocation> SnapshotFinalizerInvocations()
        {
            lock (gate)
            {
                return finalizerInvocations.ToList();
            }
        }

        public IReadOnlyList<AgentToolInvocationTrace> SnapshotToolInvocationTraces()
        {
            lock (gate)
            {
                return toolInvocationTraces.ToList();
            }
        }
    }
}
