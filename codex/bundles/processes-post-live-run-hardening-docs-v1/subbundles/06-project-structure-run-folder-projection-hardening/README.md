# SB06: Project Structure Run Folder Projection Hardening

## Status

- Status: Completed

## Objective

- Harden project-structure process run folder projection and avoid noisy artifact subtree nodes.

## Covered Inputs

- RN06 maps to RQ06.
- Preserve the original bundle scope for this subbundle.

## Prerequisites

- SB05 completed.

## Exact Source References

- repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessRunSyncBridge.cs
- repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessNodeService.cs
- repo://src/CanDoItAll.Modules.Processes/ProjectStructure/ProcessProjectStructureContext.cs
- repo://tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs

## Deliverables

- Explicit run-folder projection policy, tests, and docs.

## Dependency Impact

- SB09, SB13, and SB18 rely on navigable run folder projection.

## Validation Depth

- Critical foundation with browser proof if UI-visible projection changes.
- Include failing-first or adversarial proof when behavior changes, passing proof, source assertions, anti-stub audit, changed-file hashes, classification, and proof-debt closure status.

## Implementation Steps

- Inspect the referenced source and nearby tests.
- Implement the smallest correct change set for this subbundle only.
- Update proof artifacts under bundle://proof/SB06/.

## Do Not Do

- Do not hard-code Blazor, Tetris, project ids, run ids, local paths, or user-specific paths.
- Do not use docs-only changes to satisfy runtime proof requirements.

## Acceptance Checklist

- Deliverables are complete.
- Required tests or source assertions are recorded.
- Execution report gate rows are updated.

## Closure Evidence

- Runtime policy: repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessRunFolderProjectionPolicy.cs
- Projection consumer: repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAssemblyService.cs
- Tests: repo://tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs
- Manifest: bundle://proof/SB06/manifest.md
- Semantic invariants: bundle://proof/SB06/semantic-invariants.md
- Passing proof: bundle://proof/SB06/transcripts/passing.txt
- Adversarial proof: bundle://proof/SB06/transcripts/failing-first.txt
- Browser validation: N/A; no project-structure markup, CSS, route, canvas layout, or visible UI rendering component changed.

## Proof Required

- bundle://proof/SB06/manifest.md
- bundle://proof/SB06/semantic-invariants.md when the subbundle is critical or behavior-changing.
- Command transcripts under bundle://proof/SB06/transcripts/.

## Browser Validation Logging

- Required if project-structure UI rendering changes; capture large desktop and affected narrow-width proof.
- Update bundle://reviews/01-execution-report.md if browser proof is applicable.

## Progression Gate

- SB09 and SB13 may rely on projection only after noisy-folder negative proof passes.

## Suggested Agent Prompt

- Execute SB06 literally, preserve runtime genericity, and close owned proof before moving downstream.
