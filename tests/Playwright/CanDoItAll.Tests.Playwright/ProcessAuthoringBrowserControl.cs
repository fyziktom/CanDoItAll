using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Tests.Playwright;

internal sealed class ProcessAuthoringBrowserControl {
    internal string Evidence { get; set; } = string.Empty;
    internal int ReadCount;
    internal int DefinitionCommandCount;
    internal TaskCompletionSource DefinitionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource ReleaseDefinition { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal ProcessDefinitionEditorCommand? DefinitionCommand { get; set; }
    internal ProcessDefinitionEditorCommandResult? DefinitionResult { get; set; }
    internal ProcessDefinitionRoleEditorCommandResult? RoleResult { get; set; }
    internal ProcessDefinitionStepEditorCommand? StepCommand { get; set; }
    internal ProcessDefinitionStepEditorCommandResult? StepResult { get; set; }
}
