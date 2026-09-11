using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed record MafContextToolDeclaration(string Name, bool RequiresApproval);

internal sealed class MafContextToolRegistration {
    internal MafContextToolRegistration(IEnumerable<MafContextToolDeclaration> declarations,
        IMafContextToolSourcePreparation? sourcePreparation = null,
        Func<AgentToolResultDisclosure, CancellationToken, ValueTask<IAsyncDisposable?>>? authorizeResultDisclosureAsync = null) {
        ArgumentNullException.ThrowIfNull(declarations);
        Declarations = declarations.ToImmutableArray();
        SourcePreparation = sourcePreparation;
        AuthorizeResultDisclosureAsync = authorizeResultDisclosureAsync;
        ToolNames = Declarations.Select(declaration => declaration.Name).ToImmutableArray();
        if (ToolNames.Length == 0 || ToolNames.Any(string.IsNullOrWhiteSpace) ||
                ToolNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ToolNames.Length) {
            throw new ArgumentException("A context tool registration requires distinct nonempty tool names.", nameof(declarations));
        }
    }

    internal ImmutableArray<string> ToolNames { get; }
    internal ImmutableArray<MafContextToolDeclaration> Declarations { get; }
    internal IMafContextToolSourcePreparation? SourcePreparation { get; }
    internal Func<AgentToolResultDisclosure, CancellationToken, ValueTask<IAsyncDisposable?>>? AuthorizeResultDisclosureAsync { get; }

    internal AIFunction[] RequireContribution(IReadOnlyList<AITool> inherited, IReadOnlyList<AITool> merged) {
        var remaining = new Dictionary<AITool, int>(ReferenceEqualityComparer.Instance);
        foreach (var tool in inherited) {
            if (tool is null || ToolNames.Contains(tool.Name, StringComparer.OrdinalIgnoreCase)) {
                throw ContractFailure();
            }
            remaining.TryGetValue(tool, out var count);
            remaining[tool] = count + 1;
        }
        List<AITool> contributed = [];
        foreach (var tool in merged) {
            if (tool is null) {
                throw ContractFailure();
            }
            if (!remaining.TryGetValue(tool, out var count)) {
                contributed.Add(tool);
            } else if (count == 1) {
                remaining.Remove(tool);
            } else {
                remaining[tool] = count - 1;
            }
        }
        if (remaining.Count != 0) {
            throw ContractFailure();
        }
        return RequireTools(contributed);
    }

    internal AIFunction[] RequireTools(IEnumerable<AITool> tools) {
        var provided = tools.ToArray();
        if (provided.Any(tool => tool is not AIFunction || !ToolNames.Contains(tool.Name, StringComparer.Ordinal)) ||
                provided.Select(tool => tool.Name).Distinct(StringComparer.Ordinal).Count() != provided.Length) {
            throw ContractFailure();
        }
        foreach (var tool in provided) {
            if (Declarations.Single(declaration => declaration.Name == tool.Name).RequiresApproval !=
                    (tool.GetService<ApprovalRequiredAIFunction>() is not null)) {
                throw new AgentToolAdmissionException("tool-admission.context-tool-approval",
                    "The trusted context tool approval wrapper differs from its declared contract.");
            }
        }
        return provided.Cast<AIFunction>().ToArray();
    }

    private static AgentToolAdmissionException ContractFailure()
        => new("tool-admission.context-tool-contract",
            "The trusted context provider returned tools outside its declared function contract.");
}
