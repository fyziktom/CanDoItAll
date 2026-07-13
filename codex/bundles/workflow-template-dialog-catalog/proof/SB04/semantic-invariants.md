# SB04 Semantic Invariants

## SB04-INV-001

- Invariant ID: `SB04-INV-001`
- Source raw note: `N010`, `N012`
- Expected behavior: Former SEAMARK examples are generic offer-analysis templates without branded or sensitive terms in shipped/UI-facing content.
- Disallowed shallow implementation: Renaming only display titles while leaving company names, exact model names, prices, or source file names in prompts/tests.
- Failing-first test: Pre-change template content contained SEAMARK-specific names and details.
- Passing test: `bundle://proof/SB04/transcripts/sb04-template-unit-tests.txt`
- Changed source files: `repo://Templates/Workflows/workflows/default-workflows.yaml`, `repo://tests/CanDoItAll.Tests.Unit/WorkflowTemplatePackLoaderTests.cs`
- Production assertions: Default workflow template pack contains `Offer Document Folder Summary` and `Offer Price List Extraction` with generic instructions.
- Red-team negative case: Source search rejects `SEAMARK`, exact model names, exact source filenames, and exact prices in shipped templates/UI-facing tests.
- Downstream dependency check: Browser proof filters the live catalogue to generic Offer templates.

## SB04-INV-002

- Invariant ID: `SB04-INV-002`
- Source raw note: `N011`
- Expected behavior: The debranded workflow still performs generic offer/product-document analysis and summary extraction.
- Disallowed shallow implementation: Replacing useful analysis instructions with vague placeholder text.
- Failing-first test: Old workflow instructions were tied to a specific company/source offer.
- Passing test: `bundle://proof/SB04/transcripts/sb04-template-unit-tests.txt`
- Changed source files: `repo://Templates/Workflows/workflows/default-workflows.yaml`
- Production assertions: Generic prompts cover product families, variants, use cases, evidence, commercial terms, price uncertainty, and review flags.
- Red-team negative case: Unit test asserts forbidden exact terms are absent while expected generic offer templates are present.
- Downstream dependency check: Live catalogue screenshot shows meaningful generic descriptions.

## SB04-INV-003

- Invariant ID: `SB04-INV-003`
- Source raw note: `N009`
- Expected behavior: Catalogue and preview dialogs remain close to generated proposals while using existing CanDoItAll/BaseLib components.
- Disallowed shallow implementation: A text-only modal or a preview canvas too poorly framed to inspect the workflow.
- Failing-first test: Initial browser proof exposed a preview canvas with clipped workflow nodes.
- Passing test: `bundle://proof/SB04/transcripts/sb04-browser-validation.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.css`
- Production assertions: Preview uses a real `CanvasWorkbench`, a canvas-dominant layout, and initial pan/zoom that shows all workflow nodes.
- Red-team negative case: Final screenshot comparison notes record the corrected canvas framing and accepted proposal differences.
- Downstream dependency check: Browser screenshot proof closes SB02/SB03 deferred visual validation.

## SB04-INV-004

- Invariant ID: `SB04-INV-004`
- Source raw note: `N013`
- Expected behavior: UI validation is large-screen only.
- Disallowed shallow implementation: Claiming responsive proof without running it or accidentally spending time on out-of-scope small/medium checks.
- Failing-first test: N/A process constraint; this is a validation-scope invariant.
- Passing test: `bundle://proof/SB04/transcripts/sb04-browser-validation.txt`
- Changed source files: `bundle://reviews/01-execution-report.md`
- Production assertions: Browser validation records 1680x1000 and explicitly skips smaller viewports.
- Red-team negative case: Execution report does not claim small or medium viewport coverage.
- Downstream dependency check: Final closure uses only the large-screen screenshot artifacts.
