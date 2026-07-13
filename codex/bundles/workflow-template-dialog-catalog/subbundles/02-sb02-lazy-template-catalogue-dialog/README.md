# SB02 Lazy Template Catalogue Dialog

## Status

- `Completed`

## Objective

- Remove the primary Templates tab and add a Workflows-tab catalogue button whose dialog loads the workflow template pack only when opened.

## Success Criteria

- No Templates tab remains in the primary Workflows tabs.
- A Workflows-tab button opens the template catalogue dialog.
- The template pack is not loaded during page initialization, refresh, or unrelated tab changes.
- Catalogue dialog shows template count, seed/version metadata, names, basic descriptions, and Preview actions.

## Covered Inputs

- `N001`, `N002`, `N003`, `N004`

## Prerequisites

- SB01 closure gate passed.
- Proposal artifact `bundle://evidence/design/template-catalogue-dialog-proposal.png` exists.

## Exact Source References

- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.css`
- `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`

## Deliverables

- Workflows-tab template catalogue button.
- Lazy-loaded catalogue dialog using existing shared components.
- Explicit loading and error states.
- Component tests for removed tab, dialog open, catalogue content, and lazy load behavior.

## Dependency Impact

- SB03 depends on the dialog state, selected template, and loaded template pack from SB02.
- Weak lazy-load proof invalidates the user’s core performance/UX requirement.

## Validation Depth

- `Critical UI and lazy-loading foundation`

## Implementation Steps

1. Remove `TemplatesTabIndex` and the Templates `TabsItem`.
2. Remove template pack loading from tab-change requirements.
3. Add state and methods for opening/closing the template catalogue dialog.
4. Load `WorkflowTemplatePackLoader.Load()` only from catalogue open.
5. Add catalogue markup modeled on `template-catalogue-dialog-proposal.png`.
6. Add/update component tests.
7. Capture proof transcripts and update execution report.

## Scope Exceptions

- Preview canvas and draft adoption are SB03.
- SEAMARK debranding is SB04.

## Do Not Do

- Do not keep hidden eager template loading to make tests pass.
- Do not introduce ad hoc structural wrappers when existing `Dialog`, `Grid`, `Stack`, `Cluster`, and `Button` usage is sufficient.
- Do not silently swallow template-loading errors.

## Acceptance Checklist

- [x] Templates tab removed.
- [x] Catalogue button exists on Workflows tab.
- [x] Template loader is invoked only when the catalogue opens.
- [x] Dialogue includes template basics and Preview actions.
- [x] Tests cover the lazy-load invariant.
- [x] Large-screen browser screenshot captured in final SB04 pass.

## Proof Required

- Failing-first transcript for a test that would fail if templates load eagerly or the Templates tab remains.
- Passing component test transcript.
- Source assertion transcript showing `TemplatePackLoader.Load()` is not called from tab-change requirements.
- Anti-stub audit transcript.
- `proof/SB02/manifest.md` and `proof/SB02/semantic-invariants.md`.

## Browser Validation Logging

- Route: `/agents/workflows`
- Viewport: `>=1600x900`; no small/medium pass.
- Actions: open Workflows tab, click template catalogue button, verify dialog open, verify template list/detail and Preview buttons.
- Screenshot path: `bundle://proof/SB02/browser/workflow-template-catalogue-dialog-large.png`
- Review against `bundle://evidence/design/template-catalogue-dialog-proposal.png`.

## Progression Gate

- SB03 may start only after SB02 proves lazy loading, catalogue open state, and a selected template can be previewed.

## Suggested Agent Prompt

```text
Implement SB02 only. Remove the Templates tab, add the lazy-loaded catalogue dialog from the Workflows tab, prove the template pack is not loaded until dialog open, capture component and large-screen browser proof, and update the SB02 proof manifest and execution report.
```
