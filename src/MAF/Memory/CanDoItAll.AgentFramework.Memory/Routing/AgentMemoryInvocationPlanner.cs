using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Memory.Routing;

public enum AgentMemoryInvocationPlanDecision
{
    Skip = 0,
    Query = 1,
    Reject = 2
}

public sealed record AgentMemoryInvocationPlan(
    AgentMemoryInvocationPlanDecision Decision,
    string Query,
    IReadOnlyList<AgentMemoryProviderBindingSetting> Providers,
    string Diagnostic,
    bool TransformRequestMessages)
{
    public static AgentMemoryInvocationPlan Skip(string diagnostic) =>
        new(AgentMemoryInvocationPlanDecision.Skip, string.Empty, [], diagnostic, TransformRequestMessages: false);

    public static AgentMemoryInvocationPlan QueryProviders(
        string query,
        IReadOnlyList<AgentMemoryProviderBindingSetting> providers,
        bool transformRequestMessages) =>
        new(AgentMemoryInvocationPlanDecision.Query, query, providers, string.Empty, transformRequestMessages);

    public static AgentMemoryInvocationPlan Reject(string diagnostic) =>
        new(AgentMemoryInvocationPlanDecision.Reject, string.Empty, [], diagnostic, TransformRequestMessages: false);
}

public static class AgentMemoryInvocationPlanner
{
    public const int MaximumProviderFanOut = 8;

    public static AgentMemoryInvocationPlan Plan(
        AgentMemoryAccessSettings settings,
        string? prompt)
    {
        var access = AgentMemoryAccessMetadata.Normalize(settings);
        var directive = MemoryDirectiveParser.Parse(prompt);
        if (!directive.Success)
        {
            return AgentMemoryInvocationPlan.Reject(directive.Diagnostic);
        }

        if (access.InvocationMode == AgentMemoryInvocationMode.Disabled)
        {
            return directive.ProviderAliases.Count == 0
                ? AgentMemoryInvocationPlan.Skip("Memory invocation is disabled for this agent.")
                : AgentMemoryInvocationPlan.Reject("This agent does not allow prompt-forced memory invocation.");
        }

        if (string.IsNullOrWhiteSpace(directive.Query))
        {
            return AgentMemoryInvocationPlan.Reject("Memory invocation requires a non-empty query after its directives.");
        }

        IReadOnlyList<AgentMemoryProviderBindingSetting> selected;
        if (directive.ProviderAliases.Count > 0)
        {
            var byAlias = access.ProviderBindings.ToDictionary(
                binding => binding.Alias,
                binding => binding);
            var unknownAlias = directive.ProviderAliases.FirstOrDefault(alias => !byAlias.ContainsKey(alias));
            if (!string.IsNullOrWhiteSpace(unknownAlias.Value))
            {
                return AgentMemoryInvocationPlan.Reject(
                    $"Memory provider alias '{unknownAlias}' is not configured for this agent.");
            }

            var requestedAliases = directive.ProviderAliases.ToHashSet();
            selected = access.ProviderBindings
                .Where(binding => requestedAliases.Contains(binding.Alias))
                .ToArray();
        }
        else if (access.InvocationMode == AgentMemoryInvocationMode.ExplicitDirective)
        {
            return AgentMemoryInvocationPlan.Skip(
                $"Memory is prompt-forced for this agent. Start the prompt with '{MemoryDirectiveParser.Prefix}<alias>' to invoke it.");
        }
        else
        {
            selected = access.ProviderBindings
                .Where(binding => binding.IncludeInAutomaticContext)
                .ToArray();
        }

        if (selected.Count == 0)
        {
            return AgentMemoryInvocationPlan.Reject(
                "No memory provider is configured for the selected invocation mode.");
        }

        if (selected.Count > MaximumProviderFanOut)
        {
            return AgentMemoryInvocationPlan.Reject(
                $"Memory invocation selected {selected.Count} providers; the maximum deterministic fan-out is {MaximumProviderFanOut}.");
        }

        return AgentMemoryInvocationPlan.QueryProviders(
            directive.Query,
            selected,
            transformRequestMessages: directive.ProviderAliases.Count > 0);
    }
}
