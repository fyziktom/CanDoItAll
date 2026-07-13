# SB04 Generic Template Debranding And Large-Screen Proof

## Status

- `Completed`

## Objective

- Rename SEAMARK-specific workflow examples into generic offer-analysis templates, update tests, and close the bundle with large-screen browser proof against the generated dialog proposals.

## Success Criteria

- Workflow template pack no longer contains `SEAMARK` in template names, descriptions, routing instructions, node labels, output titles, or UI-facing smoke labels.
- Former SEAMARK workflows remain valid generic examples for offer/product-document analysis.
- Final component/unit/build proof passes.
- Large-screen browser screenshots show catalogue and preview dialogs close enough to generated proposals for the user’s UX intent.

## Covered Inputs

- `N009`, `N010`, `N011`, `N012`, `N013`

## Prerequisites

- SB03 closure gate passed.
- Draft adoption and preview dialogs work.

## Exact Source References

- `repo://Templates/Workflows/workflows/default-workflows.yaml`
- `repo://Templates/Workflows/manifest.yaml`
- `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowTemplatePackLoaderTests.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureWorkflows.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.css`

## Deliverables

- Generic renamed workflow templates.
- Updated test expectations and any UI-facing smoke labels.
- Final screenshots and screenshot comparison notes.
- Completed raw-note closure table and final validator proof.

## Dependency Impact

- This is the final closure subbundle. If debranding is shallow or screenshots reveal poor layout, earlier UI/content subbundles must reopen.

## Validation Depth

- `Critical final closure`

## Implementation Steps

1. Rename former SEAMARK workflow keys/names/descriptions/routing instructions/node labels/output asset titles to generic offer-analysis language.
2. Update tests that assert old names or labels.
3. Run source searches for `SEAMARK`, `Seamark`, and known exact sensitive strings.
4. Run targeted tests and build.
5. Capture final large-screen Playwright screenshots for catalogue and preview dialogs.
6. Compare screenshots to proposal images and record visual findings.
7. Complete proof manifests, raw-note closure, execution report, and final validators.

## Scope Exceptions

- Historical local test-data folder names outside repo-owned shipped templates may remain if not part of UI-facing output; any remaining occurrence must be documented with justification.

## Do Not Do

- Do not remove the underlying generic workflow capability.
- Do not replace debranding with vague placeholder templates.
- Do not run small or medium viewport tests.

## Acceptance Checklist

- [x] No shipped workflow template content contains SEAMARK.
- [x] Generic offer-analysis wording is meaningful for new users.
- [x] Tests and build pass.
- [x] Catalogue and preview screenshots are captured and reviewed.
- [x] Raw notes are closed as solved or explicitly marked otherwise.

## Proof Required

- Source-search transcript for debranding.
- Template validation/unit test transcript.
- Component test transcript.
- Build transcript.
- Large-screen Playwright screenshots and comparison notes.
- Red-team/fake-proof closure artifact.
- `proof/SB04/manifest.md` and `proof/SB04/semantic-invariants.md`.

## Browser Validation Logging

- Route: `/agents/workflows`
- Viewport: `>=1600x900`; no small/medium pass.
- Actions: open catalogue, verify generic template names, open preview, inspect canvas and Add to my drafts footer.
- Screenshots:
  - `bundle://proof/SB04/browser/workflow-template-catalogue-dialog-large.png`
  - `bundle://proof/SB04/browser/workflow-template-preview-dialog-large.png`
- Comparison targets:
  - `bundle://evidence/design/template-catalogue-dialog-proposal.png`
  - `bundle://evidence/design/template-preview-dialog-proposal.png`

## Progression Gate

- Bundle may close only after completed-stage validator passes and final screenshots/proof manifests exist.

## Suggested Agent Prompt

```text
Implement SB04 only. Debrand SEAMARK workflow templates into generic offer-analysis examples, update tests, run source searches, capture final large-screen screenshots compared to generated proposals, complete proof manifests, close raw notes, and run final validators.
```
