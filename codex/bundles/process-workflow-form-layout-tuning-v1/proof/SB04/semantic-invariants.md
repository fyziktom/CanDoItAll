# SB04 Semantic Invariants

## Layout Tabs Reachable

- Invariant ID: `SB04-LAYOUT-TABS-REACHABLE`
- Source raw note: `N001`, `N003`, and `N004` from `bundle://inputs/02-structured-input.md`.
- Expected behavior: the Processes step setup form exposes Basic info, Execution, Contracts, Routing, Roles, and Artifacts tabs, and the Workflows Editor inspector exposes Definition, Node setup, Routes, and Preview tabs.
- Disallowed shallow implementation: adding tab labels while leaving the original long mixed form stack, hiding child editors, or breaking existing bindings and callbacks.
- Failing-first test: N/A - process/non-production layout-only refactor with no behavior-specific failing-first test; source assertions and browser proof are the relevant closure proof.
- Passing test: `bundle://proof/SB04/transcripts/semantic-invariant-evidence.txt` exits 0 and cites source assertions plus browser proof.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepEditorForm.razor` and `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`.
- Production assertions: `bundle://proof/SB04/transcripts/source-assertions.txt` confirms the tab roots and tab test IDs, and `bundle://proof/SB04/transcripts/browser-proof.txt` records route-level browser validation.
- Red-team negative case: `bundle://proof/SB04/transcripts/anti-stub-audit.txt` rejects placeholder tab implementations.
- Downstream dependency check: targeted module builds passed in `bundle://proof/SB04/transcripts/processes-module-build.txt` and `bundle://proof/SB04/transcripts/agentframework-module-build.txt`.

## Shared Components And No Stub Layout

- Invariant ID: `SB04-SHARED-COMPONENTS-NO-STUBS`
- Source raw note: `N002` and `N005` from `bundle://inputs/02-structured-input.md`.
- Expected behavior: layout tuning uses existing shared UI components and repo-local component patterns, without page-specific styling or behavior-layer changes.
- Disallowed shallow implementation: custom one-off CSS, fake fields, placeholder branches, or generated image artifacts standing in for product code.
- Failing-first test: N/A - process/non-production layout-only refactor with no behavior-specific failing-first test; the anti-stub audit is the adversarial proof.
- Passing test: `bundle://proof/SB04/transcripts/semantic-invariant-evidence.txt` exits 0 and cites the shared-component source assertions.
- Changed source files: changed file hashes are listed in `bundle://proof/SB04/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB04/transcripts/source-assertions.txt` verifies shared tabs, compact grids, retained child editors, and retained workflow inspector tab state.
- Red-team negative case: `bundle://proof/SB04/transcripts/anti-stub-audit.txt` rejects TODO, NotImplemented, dummy, fake, and lorem ipsum markers in changed UI files.
- Downstream dependency check: browser proof verifies the rendered layouts under desktop and narrow viewports.
