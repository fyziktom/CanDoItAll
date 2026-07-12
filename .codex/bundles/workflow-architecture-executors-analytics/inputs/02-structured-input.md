# Structured Input

## Core Objective

- Deliver a more testable and extensible workflow subsystem in which built-in and plugin executors use stable contracts, agent tools and workflow nodes share cohesive operation implementations where appropriate, all intended production launch paths work, run usage/cost/duration is queryable and visible, and the large-screen workflow UI exposes every runnable executor with capability-appropriate settings.

## Success Criteria

- A documented architecture inventory and target boundary map pass the C# architecture gate.
- Adding an executor implementation or plugin-contributed executor does not require new branches in the workflow runtime or the 3,600-line canvas editor.
- Shared file, spreadsheet, document-conversion/MarkItDown, image-inspection, and command capabilities are inventoried; each missing-node decision is enumerated as implemented now, owned by a concrete subbundle, or explicitly excluded with evidence.
- Tool-facing and workflow-facing adapters delegate to one cohesive operation service where their security/lifecycle semantics allow reuse; neither adapter invokes the other transport surface.
- Project structure, scheduler, generic agent-tool, and process/subprocess launch paths are either proven production paths or implemented with typed contracts and tests.
- Workflow run analytics expose duration plus per-provider/per-model input, cached-input, output, total-token, known-cost, and unknown-usage information without silently treating missing provider usage as zero-cost success.
- Built-in and plugin executor configuration schemas reach the workflow editor; optional trusted custom renderers can be registered without string-based branching in the editor, while schema fallback remains available.
- Unit, component, integration, API, and large-screen Playwright coverage proves behavior and negative cases at the correct boundaries.

## Hard Constraints

- Preserve strict UI/application/domain/infrastructure separation and current project dependency direction; do not add a cyclic reference.
- Use strongly typed identifiers, enums, settings, and descriptors. Renderer and executor keys may cross serialization/plugin boundaries as strings only behind typed wrappers.
- Prefer the smallest cohesive extraction. Do not add partial classes, nested helper types, service location, silent fallbacks, or parallel duplicate implementations.
- Errors and unknown usage must remain explicit and actionable; sensitive settings, secrets, prompts, and external payloads must stay redacted in logs and analytics.
- Plugins remain capability-governed and cannot gain direct unrestricted workspace, secret, network, or host access through workflow execution.
- Use existing BaseLib/CanvasLib components and project CSS. Do not add Radzen unless the affected project already uses it.
- UI implementation and proof target large desktop screens only. Small and medium responsive design is explicitly out of scope for this initiative.

## Allowed Side Effects

- Production, test, migration, UI, API, and composition files named by the validated subbundles may change.
- New cohesive top-level types or narrowly justified projects may be added only when the dependency map and architecture checkpoint approve them.
- None beyond documented subbundles.

## Source Artifacts

- `bundle://inputs/00-original-request.md`.
- `bundle://inputs/01-source-artifacts.md`.
- `repo://.codex/bundles/project-structure-workflow-runs/proof/` as historical evidence only.

## Input Coverage Signals

- Architecture, implementation, and test coverage are distinct deliverables.
- Plugin-contributed executors are not an optional follow-up to built-ins.
- “Missing executor nodes” requires an explicit capability inventory; a single example node does not close it.
- “One implementation” requires evidence of the shared service owner and thin tool/workflow adapters, not merely similar tests.
- Project structure, scheduler, agent tool, and process subprocess are four separately proven launch contexts.
- Cost, tokens by model, and elapsed time are three separately visible analytics dimensions.
- Executor discoverability and executor settings rendering are separate UI requirements.
- Large-screen-only is a hard scope boundary, not a request for responsive follow-up work.

## Dependency And Sequencing Signals

- Architecture/current-state characterization unlocks all implementation subbundles.
- Shared operation ownership and executor/plugin extension contracts unlock missing executor additions and UI renderer registration.
- A typed run analytics contract and production emitter/persistence path unlock analytics UI.
- Lifecycle entry points depend on a stable workflow launch application boundary and must not each duplicate start validation.
- UI executor/settings work depends on final descriptor/schema shape and the completed executor inventory.
- Final browser and integration proof depends on all prior critical foundation gates.

## Validation Expectations

- Failing-first and passing tests for every behavior-changing critical invariant.
- Direct unit tests for extracted services/adapters without constructing the old workflow page/runtime.
- Composition smoke tests for executor catalogs, plugin descriptors/renderers, launch tools, and analytics services.
- Database/API integration proof for workflow usage persistence and aggregation.
- Large-screen real-browser proof for catalog, settings editors, run details, and analytics; no small/medium viewport work is required.
- Refreshed CodeAnalytics dependency/findings evidence and a C# architecture review gate before closure.

## Evidence Contract

- `dotnet build CanDoItAll.slnx --no-restore` or a documented restore-aware equivalent.
- Targeted unit/component/integration/Playwright commands listed per subbundle, followed by a clean broader confirmation run.
- Critical proof manifests under `bundle://proof/SBxx/` containing hashes, failing/passing transcripts, source assertions, anti-stub output, and semantic invariants.
- Browser screenshots and assertions under `bundle://proof/SB06/browser/` and final red-team closure under `bundle://proof/SB07/`.

## UI Validation Strategy

- Start at a maximized headed browser or an equivalent viewport of at least 1600x1000 on the workflow route.
- Validate catalog visibility, plugin grouping, add-node dialog, inspector/modal settings, run details, model/token/cost/duration analytics, clipping, scroll ownership, overlays, and keyboard/mouse reachability.
- Review screenshots for hierarchy, density, alignment, readable labels/values, unused space, clipping, z-index, and consistency with existing large-screen CanDoItAll surfaces.
- Do not perform small or medium responsive redesign or follow-up passes; the user explicitly excluded them.

## Browser Validation Analytics

- `reviews/01-execution-report.md` must record subbundle, route, exact viewport, actions, assertions, screenshot paths, and pass/fail result while proof is fresh.
- UI-critical SB06 must include open-state proof for node creation and settings surfaces, plus one downstream workflow save/run smoke.

## Working Assumptions

- “Markitdown” means the existing `ManagedCode.MarkItDown` C# integration and its workspace document-conversion service.
- Existing file and spreadsheet workflow executors count as implemented only when operation parity and shared-service ownership are proven; they are not automatically duplicated as one node per low-level method.
- “Agent can call it as tool” requires a generic governed workflow launch tool, even though a project-structure-specific agent tool already starts workflow nodes.
- Process “subprocess” means a production process execution adapter/assignment path that launches and tracks a workflow run, not merely the currently registered but otherwise unreferenced bridge.

## Primary Risks

- The current common Core project already references WorkflowExecutors.Core; careless operation-contract placement can deepen the inversion or create a cycle.
- Workflow usage exists on node/event payloads but is not a first-class run summary, so naïve aggregation can double-count retries, resumed events, or native/normalized duplicates.
- Plugin manifests carry schemas and renderer keys, but no production `ISettingsRendererSource` was found; loading arbitrary component type names would be a code-execution/security defect.
- The canvas editor contains dead or shadowed specialized settings branches behind a schema-first condition; editing it without characterization tests risks losing built-in fields.
- Persistent workflow stores and UI code-behind are large responsibility concentrations; this initiative must extract only owned slices rather than perform a big-bang rewrite.
