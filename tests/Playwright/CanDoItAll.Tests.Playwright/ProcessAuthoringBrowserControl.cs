using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Tests.Playwright;

internal sealed class ProcessAuthoringBrowserControl {
    internal string Evidence { get; set; } = string.Empty;
    internal int ReadCount;
    internal int DefinitionCommandCount;
    internal int RoleCommandCount;
    internal int ImportCommandCount;
    internal bool DelayFirstRoleResponse;
    internal bool DelayFirstImportResponse;
    internal bool IncludeImportArtifact;
    internal bool LoseFirstCommitResponse;
    internal bool FailReadAfterFirstCommit;
    internal int PendingReadFailures;
    internal TaskCompletionSource DefinitionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource ReleaseDefinition { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource RoleStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource ReleaseRole { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource ImportStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource ReleaseImport { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal ProcessDefinitionEditorCommand? DefinitionCommand { get; set; }
    internal ProcessDefinitionEditorCommandResult? DefinitionResult { get; set; }
    internal ProcessDefinitionRoleEditorCommandResult? RoleResult { get; set; }
    internal ProcessDefinitionStepEditorCommand? StepCommand { get; set; }
    internal ProcessDefinitionStepEditorCommandResult? StepResult { get; set; }
    internal ProcessTemplateImportCommand? ImportCommand { get; set; }
    internal ProcessTemplateImportCommandResult? ImportResult { get; set; }
}
