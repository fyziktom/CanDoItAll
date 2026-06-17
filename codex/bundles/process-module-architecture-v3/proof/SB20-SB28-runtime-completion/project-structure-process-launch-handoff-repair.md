# Project-Structure Process Launch Handoff Repair

Date: 2026-06-17

## Scope

This proof covers the repair for starting a process from the TetrisGame project-structure `Main App` node and the follow-on subprocess evidence handoff.

## Fixed Behaviors

- The project-structure add-process modal now receives process definitions; Playwright proof captured the populated dropdown and the selected `Multi-team software delivery and release governance` option.
- Project-structure process launch variables now include whole-project context, not only the sparse selected node. The `Main App` launch carries `ProjectStructureContextSummary` plus `OutputRoot`, `ProductRoot`, and `ExternalTargetRoot` resolved from the architecture/output-folder nodes.
- Process launch initializes the managed run artifact root before dispatch. The Tetris run root contained `steps`, `logs`, and `screenshots` before the first agent step wrote evidence.
- Subprocess launch results now include `ChildManagedArtifactRoot`, `ChildStepsArtifactRoot`, `ChildLiveProcessesRoute`, and `ExpectedChildEvidenceRefs`, so parent process steps can read child evidence from the child run root instead of incorrectly requiring child artifacts under the parent run root.
- Subprocess parent prompt guidance now explicitly says artifacts under `ChildManagedArtifactRoot` are the child evidence bundle.

## Live Dev DB Evidence

- Project id: `3324868f-66e2-478a-bb8f-14f32a5db1e9`
- Selected node: `custom:cfd406780f034384a70ea6b87507422a` (`Main App`)
- Multiteam process definition id: `3458e5d8-36b4-1861-83b1-522604c8e302`
- Pre-handoff-fix run: `add70022-e062-451f-85bd-4c30caf9eeed`
  - `feature-intake` completed and wrote `artifacts/process-runs/add70022-e062-451f-85bd-4c30caf9eeed/feature-intake/scope-packet.md`.
  - Architecture child subprocess run `f2a4d3ee-a93d-4439-a768-4c07065caff6` completed all 4 child steps.
  - Parent `architecture-review` blocked because it looked only under the parent run root, while child artifacts existed under `artifacts/process-runs/f2a4d3ee-a93d-4439-a768-4c07065caff6`.
- Post-handoff-fix fresh run: `83cc6023-5990-4f95-9999-0cd8610f77d6`
  - Launched through the project-structure UI after app restart on `http://localhost:5032`.
  - HR assignment review exposed an enabled `Review and start` button.
  - Launch variables carried `C:\programovani\dotnet\output` and the whole `ProjectStructureContextSummary`.
  - Feature-intake execution advanced through provider states and remained live at the time proof was closed; no app crash or denied-tool evidence was observed.

## Browser Proof Files

- `tetris-process-dropdown-options-after-context-fix.json`
- `tetris-process-dropdown-multiteam-selected-after-context-fix.png`
- `tetris-hr-review-after-context-fix.md`
- `tetris-hr-review-after-context-fix.png`
- `tetris-controls-after-subprocess-handoff-fix.json`
- `tetris-structure-before-new-run-subprocess-handoff-fix.md`
- `tetris-hr-after-subprocess-handoff-fix.json`

## Validation

- CodeAnalytics MCP snapshot `snap-20260617015748-8a607416`: completed, no blocking errors.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ProcessLaunchPromptTests|FullyQualifiedName~AgentToolInvocationPolicyTests.ProjectStructureToolInventory" -p:UseSharedCompilation=false`: passed 3/3.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProjectStructureAgentIntegrationTests.StartProcess" -p:UseSharedCompilation=false`: passed 5/5.
