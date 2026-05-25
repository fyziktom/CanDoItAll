# Source Observations

## Branch And Database Context

- Branch: `development`
- Reviewed commit: `62dfbdd68bc84cd74f852f3e40a5f42a2183174c`
- Commit title: `Merge branch 'db-remove-sqlite' into development`
- Interpretation: this bundle must not include SQLite remediation work. PostgreSQL is the canonical runtime target for this process hardening pass.

## Bundle Skill Format Sources

- `repo://codex/skills/bundles/candoitall-bundle-preparation/SKILL.md`
- `repo://codex/skills/bundles/candoitall-bundle-workflow/SKILL.md`

These skills require structured bundle sections, subbundle READMEs with exact source references, dependency-aware phase gates, semantic adequacy gates for critical foundations, and artifact-backed proof manifests.

## Processes Module Sources Reviewed

- `repo://src/CanDoItAll.Modules.Processes/README.md`
- `repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Current Runtime Signals Observed

- `ProcessRunAutomationDispatchService.DispatchAsync` claims step work and routes between subprocess, workflow-backed execution, and direct AgentFramework execution.
- Direct AgentFramework execution calls `ProjectExecutionArtifactsAsync`, then `TryRecoverMissingCompletionArtifactsAsync`, then transitions the step.
- Workflow-backed execution calls `workflowRunCoordinator.TryRunOrObserveAsync`; if handled, `HandleWorkflowExecutionOutcomeAsync` transitions the process step directly.
- `HandleWorkflowExecutionOutcomeAsync` does not currently show the same artifact projection/recovery/finalization calls as the direct AgentFramework path.
- `TryRecoverMissingCompletionArtifactsAsync` only operates when the execution outcome is `Completed` and required artifact expectation ids are still absent from `candidate.RecordedArtifactExpectationIds`.
- `TryRecoverStrandedMissingCompletionArtifactsAsync` exists for an `InProgress` step with a recoverable execution run and missing required artifacts.
- `DispatchCandidate` stores `RecordedArtifactExpectationIds` and `ExternalReferenceKeys` as mutable `HashSet` instances inside an otherwise record-like object.
- Artifact projection mutates those sets after successful record creation.
- Manager recovery creates a `recoveryCandidate = candidate with { ... }`, runs the manager, projects manager artifacts, then checks the original candidate's recorded expectation ids.
- The current design therefore relies on shared mutable HashSet references across candidate copies.
- Artifact projection can record artifacts from execution artifacts, workspace writes, existing managed files, final assistant response text, provider-native browser outputs, and auto decision summaries.
- Several missing-path or unreadable-file projection failures are logged and skipped instead of turned into durable artifact diagnostics.
- The test suite already includes useful artifact matching regressions: exact path matching, Playwright scratch rejection, provider-native browser snapshot rejection for regression evidence packs, governed project-structure artifact paths, and workspace receipt extraction.
