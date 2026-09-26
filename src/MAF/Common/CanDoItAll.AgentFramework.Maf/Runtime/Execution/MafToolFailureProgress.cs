namespace CanDoItAll.AgentFramework.Maf;

/// <summary>
/// Builds the execution-stream line for a tool call that reported failure, so the chat shows why a
/// tool failed without waiting for the model to repeat it. The text is the tool's own agent-safe
/// message: its first sentence first (the chat preview shows two sentences), then the typed code.
/// </summary>
internal static class MafToolFailureProgress
{
    private const int MaximumReasonLength = 240;

    public static string? Describe(string? toolName, object? result)
    {
        if (!MafRuntimeToolInvocationResultClassifier.IsExplicitFailure(result))
        {
            return null;
        }

        var tool = string.IsNullOrWhiteSpace(toolName) ? "A tool" : toolName.Trim();
        var reason = FirstSentence(MafRuntimeToolInvocationResultClassifier.ResolveFailureMessage(result));
        var code = MafRuntimeToolInvocationResultClassifier.ResolveFailureCode(result);
        return string.IsNullOrWhiteSpace(code)
            ? $"{tool}: {reason}"
            : $"{tool}: {reason} [{code}]";
    }

    private static string FirstSentence(string text)
    {
        var normalized = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var end = normalized.IndexOf(". ", StringComparison.Ordinal);
        var sentence = end < 0 ? normalized : normalized[..(end + 1)];
        return sentence.Length <= MaximumReasonLength
            ? sentence
            : sentence[..MaximumReasonLength].TrimEnd() + "…";
    }
}
