using CanDoItAll.Modules.Workbench.ProjectStructure;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Modules.Workbench;

// A tool composed for a process pre-dispatch inventory keeps its contract (name, description, schema) so the
// Processes preflight can match the step's required tool names, but it can never execute: the inventory context has
// no saved execution identity, admission or project scope, and the actual dispatch composes its own tools from the
// saved lineage.
internal sealed class ProjectStructureInventoryOnlyTool(AIFunction innerFunction) : DelegatingAIFunction(innerFunction)
{
    internal const string ErrorCode = "ProcessToolInventoryNotExecutable";

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        => throw new ProjectStructureAgentException(
            403,
            ErrorCode,
            "This project-structure tool was composed for a process pre-dispatch tool inventory without an execution identity and cannot be invoked. Dispatch composes its own tools from the saved process execution.");

    internal static IReadOnlyList<AITool> WrapAll(IReadOnlyList<AITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        var wrapped = new AITool[tools.Count];
        for (var index = 0; index < tools.Count; index++)
        {
            wrapped[index] = tools[index] is AIFunction function
                ? new ProjectStructureInventoryOnlyTool(function)
                : throw new InvalidOperationException(
                    $"Tool '{tools[index].Name}' is not a function and cannot be composed as an inert inventory tool.");
        }

        return wrapped;
    }
}
