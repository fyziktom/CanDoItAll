# SB02 Semantic Invariants

## SB02-INV-001

- Invariant ID: `SB02-INV-001`
- Source raw note: `N001`, `N002`
- Expected behavior: The primary Workflows tabs no longer expose a Templates tab; templates are opened by a Workflows-tab button.
- Disallowed shallow implementation: Hiding the Templates tab visually while leaving the old tab index and load path active.
- Failing-first test: `bundle://proof/SB02/transcripts/sb02-failing-first-component-tests.txt`
- Passing test: `bundle://proof/SB02/transcripts/sb02-passing-component-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.css`
- Production assertions: Workflows tab exposes `workflows-open-template-catalogue`; no `workflows-tab-templates` remains.
- Red-team negative case: Component test asserts the removed tab is absent and the button exists in the Workflows tab flow.
- Downstream dependency check: SB03 preview opens from the SB02 catalogue state.

## SB02-INV-002

- Invariant ID: `SB02-INV-002`
- Source raw note: `N003`, `N004`
- Expected behavior: The template pack is loaded only from catalogue open, and the catalogue shows names, descriptions, seed metadata, graph facts, and Preview actions.
- Disallowed shallow implementation: Loading templates during page initialization, refresh, unrelated tab changes, or component-library warmup.
- Failing-first test: `bundle://proof/SB02/transcripts/sb02-failing-first-component-tests.txt`
- Passing test: `bundle://proof/SB02/transcripts/sb02-passing-component-tests.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`
- Production assertions: `OpenTemplateCatalogueDialogAsync` is the loader boundary; load failures are shown in the dialog.
- Red-team negative case: Malformed temporary template pack does not break page load until the catalogue button is clicked.
- Downstream dependency check: SB04 browser proof validates the final catalogue dialog at large-screen size.
