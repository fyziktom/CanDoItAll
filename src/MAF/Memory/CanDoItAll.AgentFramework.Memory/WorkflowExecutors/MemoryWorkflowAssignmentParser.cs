using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.AgentFramework.Memory;

internal sealed record MemoryWorkflowAssignmentParseResult(
    IReadOnlyList<MemoryProviderAssignment> Assignments,
    string? Diagnostic);

internal static class MemoryWorkflowAssignmentParser
{
    public static MemoryWorkflowAssignmentParseResult Parse(
        IReadOnlyList<MemoryWorkflowProviderAssignmentSetting> settings)
    {
        try
        {
            var assignments = settings.Select(setting => new MemoryProviderAssignment(
                Enum.IsDefined(setting.Scope)
                    ? setting.Scope
                    : throw new ArgumentException($"Unsupported assignment scope '{setting.Scope}'."),
                RequireText(setting.Key, "assignment key"),
                MemoryProviderInstanceId.Parse(RequireText(
                    setting.ProviderInstanceId,
                    "assignment provider instance id"))))
                .ToArray();
            var duplicate = assignments
                .GroupBy(assignment => assignment.Scope)
                .SelectMany(group => group.GroupBy(
                    assignment => assignment.Key,
                    StringComparer.OrdinalIgnoreCase))
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate is not null)
            {
                var first = duplicate.First();
                return new MemoryWorkflowAssignmentParseResult(
                    [],
                    $"Memory workflow assignment '{first.Scope}:{first.Key}' is configured more than once.");
            }

            return new MemoryWorkflowAssignmentParseResult(assignments, Diagnostic: null);
        }
        catch (ArgumentException exception)
        {
            return new MemoryWorkflowAssignmentParseResult([], exception.Message);
        }
    }

    private static string RequireText(string? value, string description) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"Memory workflow {description} cannot be empty.")
            : value.Trim();
}
