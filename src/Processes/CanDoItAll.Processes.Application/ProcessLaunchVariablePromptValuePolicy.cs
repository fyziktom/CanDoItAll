namespace CanDoItAll.Processes.Application;

internal static class ProcessLaunchVariablePromptValuePolicy
{
    internal const int MaximumInlineContractCharacters = 8000;

    internal static bool IsAtomicContractKey(string key)
        => key.EndsWith("Contract", StringComparison.OrdinalIgnoreCase);

    internal static string CreateAtomicOmission(
        string key,
        int actualCharacters,
        int maximumCharacters)
        => $"[typed launch contract '{key}' omitted atomically; {actualCharacters} characters exceed the {maximumCharacters}-character inline budget; do not infer or reconstruct a partial contract]";
}
