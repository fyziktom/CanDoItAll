using System.Reflection;
using A2A;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

public sealed class Maf16CapabilityReflectionTests
{
    [Fact]
    public void Maf16_symbols_are_classified_from_loaded_runtime_assemblies()
    {
        var assemblies = new[]
        {
            typeof(AIAgent).Assembly,
            typeof(MessageAIContextProvider).Assembly,
            typeof(ApprovalRequiredAIFunction).Assembly,
            typeof(WorkflowBuilder).Assembly,
            typeof(A2ACardResolver).Assembly,
            typeof(AgentCard).Assembly
        }
        .Distinct()
        .ToArray();

        var availableTypeNames = assemblies
            .SelectMany(GetLoadableTypes)
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        var assemblyVersions = assemblies.ToDictionary(
            assembly => assembly.GetName().Name ?? string.Empty,
            assembly => assembly.GetName().Version?.ToString() ?? string.Empty);

        Assert.Contains(
            assemblyVersions,
            assembly => assembly.Key.StartsWith("Microsoft.Agents.AI", StringComparison.Ordinal) &&
                        assembly.Value.StartsWith("1.6.2.", StringComparison.Ordinal));
        Assert.Contains(
            assemblyVersions,
            assembly => assembly.Key.Contains("Workflows", StringComparison.Ordinal) &&
                        assembly.Value.StartsWith("1.6.2.", StringComparison.Ordinal));
        Assert.Contains("MessageAIContextProvider", availableTypeNames);
        Assert.Contains("ApprovalRequiredAIFunction", availableTypeNames);
        Assert.Contains("WorkflowBuilder", availableTypeNames);
        Assert.Contains("AgentWorkflowBuilder", availableTypeNames);
        Assert.Contains("AgentCard", availableTypeNames);
        Assert.Contains("A2ACardResolver", availableTypeNames);

        Assert.DoesNotContain("IChatMessageInjector", availableTypeNames);
        Assert.DoesNotContain("AgentSessionFiles", availableTypeNames);
        Assert.DoesNotContain("SkillFrontmatter", availableTypeNames);
        Assert.DoesNotContain("OpenTelemetryChatClient", availableTypeNames);
    }

    private static IReadOnlyList<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types
                .OfType<Type>()
                .ToArray();
        }
    }
}
