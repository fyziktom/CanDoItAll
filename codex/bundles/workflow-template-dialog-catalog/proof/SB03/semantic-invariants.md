# SB03 Semantic Invariants

## SB03-INV-001

- Invariant ID: `SB03-INV-001`
- Source raw note: `N005`, `N006`
- Expected behavior: Preview opens a separate dialog with a read-only `CanvasWorkbench`; persistence happens only when `Add to my drafts` is clicked.
- Disallowed shallow implementation: Saving a workflow during preview open or showing a static mock instead of the real canvas.
- Failing-first test: `bundle://proof/SB03/transcripts/sb03-failing-first-component-tests.txt`
- Passing test: `bundle://proof/SB03/transcripts/sb03-passing-component-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`
- Production assertions: Preview creates transient definition/component state; `AddSelectedTemplateToDraftsAsync` is the only persistence path.
- Red-team negative case: Component test asserts preview renders a canvas without adding a definition.
- Downstream dependency check: SB04 browser proof validates the final preview canvas and Add action.

## SB03-INV-002

- Invariant ID: `SB03-INV-002`
- Source raw note: `N007`
- Expected behavior: Draft adoption keeps `WorkflowLifecycleStatus.Draft` and resolves duplicate names as base, `01 <base>`, `02 <base>`, etc.
- Disallowed shallow implementation: Appending random suffixes, overwriting existing definitions, or silently choosing an unrelated fallback.
- Failing-first test: `bundle://proof/SB03/transcripts/sb03-failing-first-component-tests.txt`
- Passing test: `bundle://proof/SB03/transcripts/sb03-passing-component-tests.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`
- Production assertions: `ResolveTemplateDraftName` returns the first available deterministic prefix and throws if all `01` through `999` names are taken.
- Red-team negative case: Component test pre-seeds base and `01` names, then verifies `02 <base>` is created.
- Downstream dependency check: SB04 focused component regression reruns the duplicate-name test.
