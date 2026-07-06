using CanDoItAll.AgentFramework.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class RequiredFinalizerCapturedException(string toolName) : Exception(
    $"Required finalizer tool '{toolName}' was captured.")
{
    public string ToolName { get; } = toolName;
}

internal sealed class RepeatedToolInvocationGuard
{
    private const int MaxRepeatedToolInvocationCount = 3;
    private readonly Dictionary<string, int> repeatedToolInvocationCounts = new(StringComparer.OrdinalIgnoreCase);
    private int mutationGeneration;

    public void Guard(ToolCallContent toolCall)
    {
        var toolName = MafToolInvocationArgumentFormatter.ResolveToolName(toolCall);
        if (!ShouldGuardRepeatedToolInvocation(toolName))
        {
            return;
        }

        var signature = MafToolInvocationArgumentFormatter.ResolveToolInvocationSignature(toolCall);
        if (IsValidationToolInvocation(toolName))
        {
            signature = $"{signature}|mutationGeneration={mutationGeneration}";
        }

        var repeatedToolInvocationCount = repeatedToolInvocationCounts.TryGetValue(signature, out var currentCount)
            ? currentCount + 1
            : 1;
        repeatedToolInvocationCounts[signature] = repeatedToolInvocationCount;
        if (repeatedToolInvocationCount > MaxRepeatedToolInvocationCount)
        {
            throw new InvalidOperationException(
                $"Agent repeated identical tool invocation '{signature}' {repeatedToolInvocationCount} times in one run. Stop repeating the same tool call and either call the required next validation tool, inspect and change the underlying cause, or return a governed blocked/failed outcome.");
        }

        if (IsMutationToolInvocation(toolName))
        {
            mutationGeneration++;
        }
    }

    private static bool ShouldGuardRepeatedToolInvocation(string toolName)
    {
        return IsValidationToolInvocation(toolName) || IsMutationToolInvocation(toolName);
    }

    private static bool IsValidationToolInvocation(string toolName)
        => AgentToolInvocationPolicyMetadata.IsValidationTool(toolName);

    private static bool IsMutationToolInvocation(string toolName)
        => AgentToolInvocationPolicyMetadata.IsMutationTool(toolName);
}
