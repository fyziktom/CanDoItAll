# SB03 Proof Manifest

## Subbundle

- Subbundle: `SB03 Template Preview Canvas And Draft Adoption`
- Status: `Completed`
- Owned raw notes: `N005`, `N006`, `N007`
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`

## Changed-File Manifest

- Production source:
  - `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`
  - `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`
  - `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.css`
- Tests:
  - `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`
- Inline SHA-256 proof:
  - `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor` SHA-256 `F0149D44EDD96C62DD1B678BD620DBCE7C4EDB5A383ADBA3551ACA3F1D2E0F9B`

## Command Transcripts

- Failing-first component proof: `bundle://proof/SB03/transcripts/sb03-failing-first-component-tests.txt`
- Passing component proof: `bundle://proof/SB03/transcripts/sb03-passing-component-tests.txt`
- Source assertions and anti-stub audit: `bundle://proof/SB03/transcripts/sb03-source-assertions.txt`

## Failing-First Proof

- The pre-change targeted component run failed because the preview dialog and Add to my drafts action did not exist.

## Passing Proof

- Passing transcript: `bundle://proof/SB03/transcripts/sb03-passing-component-tests.txt`
- The post-change targeted component run passed 2/2 tests.
- Covered behavior: preview canvas renders without saving, preview surface has no editor Save button, add-to-drafts creates a draft, and existing base plus `01` names create `02 <base>`.

## Source Assertions

- Preview open creates transient state only.
- Persistent save is isolated to `AddSelectedTemplateToDraftsAsync`.
- Saved definitions use `WorkflowLifecycleStatus.Draft`.
- Draft naming uses `ResolveTemplateDraftName`.

## Anti-Stub Audit

- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/sb03-source-assertions.txt`
- No `TODO`, `NotImplemented`, or preview-pending placeholder code is present in modified Workflows page sources.

## Browser Proof

- Deferred to final large-screen Playwright pass after SB04 debranding.
- Expected preview screenshot path: `bundle://proof/SB04/browser/workflow-template-preview-dialog-large.png`.

## Production Behavior Artifact Matrix

- UI state: `isTemplatePreviewDialogOpen`, `templatePreviewTemplate`, `templatePreviewDefinition`, `templatePreviewComponent`, `templatePreviewCanvasUiState`, and `templatePreviewSelectedNodeId`.
- Persistent data: `AddSelectedTemplateToDraftsAsync` saves one LLM component and one draft workflow definition.
- Runtime/data loading: preview open does not load or persist component library data; add-to-drafts explicitly loads component library to choose provider/model metadata.
