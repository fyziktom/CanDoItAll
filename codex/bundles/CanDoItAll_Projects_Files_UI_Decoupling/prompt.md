# Codex GPT-6 Astra Max — Projects Files P2

You are the senior C#/.NET and Blazor engineer continuing CanDoItAll's incremental component/UI decoupling. Implement the work, build it and validate the actual production composition. Do not stop at planning or at the first small repair.

## Mission and scope

Preserve the completed Workspace and Projects P1 boundaries. First close **P1-R1: known lifetime refusal misclassified as an unknown write** and **P1-R2: failed JS import rethrown during editor disposal**, then finish **both Projects Files renderers** and an independent, useful UI sandbox. The production module can continue using in-process application owners; this is not an API-only conversion. Do not extract Workbench, Processes or another module in this run.

Reviewed main branch: `components-decoupling`, `c207b499c3165ba95e46ffaf518884097d3135dd`. Reviewed Components development: `4a858412d2c2a3f6123bf23d8c4584f05b47627d`. FileTools source reviewed: `3a080ecd31068a77c1e1bd639f7a78e21c93db85`. These identify evidence, NOT mandatory execution pins. Use the actual current checkout; record drift, preserve unrelated edits and do not reset, force-checkout or recreate already extracted projects.

## Read first

Read the current repository `AGENTS.md`, `.github/copilot-instructions.md`, `docs/testing.md`, `.github/workflows/ci.yml`, `docs/architecture/ui-component-seams.md`, the applicable family standards and these package inputs:

1. [Shared foundation](shared/README.md), its four architecture documents and [shared validation](shared/VALIDATION.md).
2. [P1 review](P1_REVIEW.md), [refusal repair](S0_KNOWN_REFUSAL.md), [JS lifecycle repair](S0_FORM_JS_LIFETIME.md), [P2 scope](SCOPE_AND_ARCHITECTURE.md) and [Files source review](FILES_SOURCE_REVIEW.md).
3. [Validation matrix](VALIDATION_MATRIX.md), [application journeys](APPLICATION_JOURNEYS.md), [development loop](DEV_LOOP.md), [desktop policy](LARGE_SCREEN_POLICY.md) and [execution/closure](EXECUTION_AND_CLOSURE.md).

Use available code-analytics and Components MCPs to locate consumers and actual component contracts. If an expected MCP is unavailable, record it once and use evaluated MSBuild graphs, exact code searches, CLI discovery and the real source; do not spend the run repeatedly trying unavailable tooling. All new code comments, product strings, developer docs and commit messages are English. The final user-facing summary may be Czech.

## Stage E0 — establish the boundary before editing

Inventory the actual branch, dirty files, sibling revisions, source-reference mode, SDK, app/test configurations and isolated test resources. Read the P1 evidence record. Do not repeat the entire historical Workspace closure campaign merely to recreate old counts. The shared-provider selector remains `REVALIDATED_WITHOUT_ROOT_CAUSE`; reopen it if it recurs or if this run changes the implicated renderer/provider path, not as an unrelated paid prerequisite.

Evaluate the existing Projects.UI/Projects.Contracts/P1 sandbox closures and all existing protected UI/sandbox roots. The previous report's five/six-project P1 closures are observations, not hard-coded assertions for every environment. Preserve their dependency direction. Files must not pull Integration implementations, Infrastructure or FileTools runtime registrations into those already lean roots.

Confirm Components development is already published; do not ask the operator to repeat that push. Verify the actual assembly/static-asset provenance used by this build and the existing CI branch-resolution rule. A development-targeted consumer and an unmatched direct feature-branch workflow are not the same dependency-resolution case. No remote write, PR, merge or package publication is authorized by this package.

## Stage S0 — fix the two bounded P1 gaps

Follow P1-R1 exactly. The reviewed native seed path checks the original ProjectWriteAdmission before native writes. The deletion path checks its exact expected admission before participant preparation. Their typed refusal is not an unknown commit. The page's generic catch currently maps those refusals to SeedOutcomeUnknown/DeleteOutcomeUnknown and leaves a per-public-ID mutation slot occupied.

Extend the real-owner seed test and add the corresponding exact-delete test before changing code. Assert the truthful UI disposition, retained prior project acknowledgement, unchanged successor data, and explicit acquisition of the new lifetime with the same public ID. A known rejected old operation must not indefinitely block that fresh lifetime. Do not make the old draft writable against the replacement. Preserve genuine lost-acknowledgement locks and exactly-once publication of known results.

Choose a small typed state/phase adaptation at the existing presentation/owner boundary. Do not catch all InvalidOperationException or clear every pending slot on refresh. Confirm that the exact exception actually came from the pre-write admission of this operation. Preserve the original operation's identity and any earlier committed project fact. Do not change the backend admission policy, schema, delete transaction, creation reservation or admitted batch semantics.

Also close P1-R2. The form caches an import task; an ordinary JSException is handled with a Close/reopen message, but Dispose re-awaits that faulted task and only catches JSDisconnectedException. Reproduce this through the actual modal. Make ownership of pending import and loaded module explicit; a previously handled failed import must not become a new unhandled disposal failure. A late acquired module must be released once, without stale validation/focus/save into a successor. Preserve real form validation and active-error diagnostics; no blanket JS catch or change to the shared Dialog contract.

Once both bounded fixes and their owning controls pass, continue with P2. A newly demonstrated critical authority/data-integrity problem is a stop condition for its affected path; do not bury it inside a visual extraction.

## Stage P2 — real Files renderers and real production wiring

Extract the entire rendered family currently in:

- `src/Modules/CanDoItAll.Modules.Projects/Pages/Components/ProjectFilesDialog.razor`
- `src/Modules/CanDoItAll.Modules.Projects/Pages/Components/ProjectFilesPortfolioPane.razor`

Include source/search status, empty/loading/error/retry states, the actual FileBrowser, the actual read-only FileInteraction, preview/back behavior already shipped by each surface, host actions and their feedback. Move owned CSS/JS/static-web-asset/Tailwind inputs with the rendering responsibility. Keep the native viewer stretch/visibility correction from P1. Do not leave the complete markup behind a one-line forwarding renderer.

Prefer `src/UI/CanDoItAll.Projects.Files.UI` and `src/Sandboxes/CanDoItAll.Projects.Files.UiSandbox`. A separate `CanDoItAll.Modules.Projects.Files.Contracts` project is optional if a real shared contract boundary needs it. Small render-only contracts may live with the renderer. Reuse existing neutral FileTools abstractions rather than copying them. Do not move file session/authority implementation classes wholesale into Contracts. Do not add FileTools dependencies to the broadly consumed Projects.Contracts just to avoid a small Files boundary.

Keep the P1 board's typed `FilesContent` slot. The Projects module composes the new Files renderer through a genuine effect host. That host owns the real coordinators, profile/actor context, accepted sessions, native launch/download authorization and exact resource lifetimes. Navigation and agent context remain at the existing route boundary. A presentation record plus intents or a cohesive view contract are both acceptable; no universal base class, service-bag facade, hidden IServiceProvider or one-interface-per-method quota.

The renderer may consume a real neutral `IFileBrowserSession` and a read-only content-source contract. Those objects provide actual rendering behavior, not authority to resolve arbitrary paths. The effect host retains the releaser/grant and concrete FileTools integration services. Actual file APIs and current admission stay with existing owners. Do not create an HTTP layer, second storage registry, new receipt system, raw-path fallback or replacement file browser.

## Required lifetime and consistency properties

Capture original project/projection, profile/actor context, session identity, revision, item and callback before the first await. Each accepted open, source refresh, preview and host action has an explicit original owner. A→B→A is a new activation, not permission for the first A to complete into the third.

Detach exact resources and callbacks before awaiting cleanup. A late reset must never clear or dispose a newer workspace or preview. A stale successful acquisition must release its own grant/session. A stale failure must not overwrite another view's error, source set, fingerprint or busy state. Scope snapshot callbacks to the bound session. Recheck after cleanup and owner waits before publication, navigation or JS delivery. Cancel old reads cooperatively, but keep their cancellation source alive until its admitted operation no longer uses it. Repeated retirement is idempotent.

Source refresh needs more than an outer UI generation check: the reviewed portfolio coordinator mutates an existing workspace. Its browser providers, scope map, action capabilities, project/source counts and revision must describe the same accepted source set. Choose a small staged/serialized update or safely owned replacement that preserves valid current-source navigation; prevent older resolution from mutating a newer session. Do not block all future selection indefinitely behind a non-cooperating old request. Read operations never replay native actions.

A successful empty projection is different from unavailable data. It must retire a preview that no longer belongs to the view. Retained stale data may be displayed explicitly as stale, but must not silently authorize new actions in another context. A removed source/item or changed binding cannot be activated through a historical callback. Preserve existing current-authority checks; a hash or UI key is not a grant.

The FileBrowser component only owns its subscriptions and binding lifetime; it does NOT dispose the supplied browser session. The production host must therefore own that session separately. Read-only preview owns an independent content grant: it can survive removal of the browser component, then must be revoked on its own retirement. Do not confuse replacement of the rendered browser with disposal of the whole workspace needed by permitted preview actions.

Attempt every owned cleanup step even if another fails. Retain safe primary and cleanup diagnostics without masking the first failure. Expected retirement/disconnection is not an active-operation success; do not globally suppress JS/IO errors or leak resources to avoid an exception. Do not invoke a successor's Closed callback after awaiting an old close. Do not close unrelated dialogs globally.

## Behavior that must stay unchanged

Retain the source limit of 64, page size 50, progressive-search limits (32 containers, 2,000 items, five seconds, one concurrent request, 200 retained matches and 2 MiB retained search state), existing source-native sort, disabled search-state retention and 280 ms UI debounce unless the current owner has intentionally changed them. Do not confuse the search budget with the 4 MiB preview content limit. Reject exceeded bounds rather than silently truncating an authority-bearing source set.

Keep read-only preview, registered viewer policy, keyboard activation and pointer double-click's existing internal-versus-local-open decision. Preserve Open in preferred application, Open containing folder and Download where actual capabilities allow them. The existing action service reauthorizes against current context, storage and trusted path; the host must not invent its own filesystem authorization or enable unavailable native launch in headless mode.

For download retain a fresh authorized lease, bounded stream handling, sanitized file name and actual browser transfer. Release the download and stream even on JS failure or cancellation. A browser download acknowledgement does not by itself prove the OS durably saved bytes; the browser test verifies the captured file. An accepted native launch may already have happened when its reply is lost or the view closes: never claim rollback or automatically retry it.

Keep the P1 editor, package owner, hierarchy filtering/fingerprint, deletion cleanup identity, current project lifetime and original admitted seed behavior. No new upload, rename, edit/save-file, recursive deletion, project transfer, provider feature, system association or blanket permission is part of P2.

## Sandbox and development loop

The new independent sandbox renders these exact components with a real FileBrowserSession over bounded synthetic providers and real FileInteraction over safe fixture content. It needs no production module, DB, vault, native launcher, inference or arbitrary disk root to render. Simulated actions must be explicitly identified; do not market them as native authority proof. Include controlled completion/retirement scenarios, missing and removed sources, current/late errors, actual preview/back and action states, two simultaneous surfaces and all supported fixture viewer families. Do not copy the real production state machine into a misleading divergent simulation.

Preserve standalone publish, CSS isolation, material fonts and actual registered file viewers. Verify the served assets, not only source files. Keep P1's sandbox and existing Workspace/Resources sandboxes independent. Measure evaluated graph, watch set, startup/hydration and Razor/C#/CSS/JS edit-to-visible changes before/after with comparable configuration and owned hosts. Three meaningful samples are enough; retain failed probes, distinguish first and warm samples, and restore probe bytes. Do not promise uniformly faster Razor or a small whole-Web build.

All new UI checks use 1920×1080, 100% zoom. 1600×1000 is optional only for a concrete large-desktop functional issue. No small/medium screen matrix, responsive tuning, mobile redesign or repeated sibling responsive campaign. Preserve established required tests and report their true result; do not delete them to save time.

## Validation and closure

Use the matrix and real application journeys. Build every changed production project; perform build-backed discovery with the same configuration/filter and confirmed count as execution. Never inherit P1 test counts or use old binaries because source HEAD matches a report. Keep fast neutral tests in a light test project selected by Components/Stable and relevant CI lists, not in the product solution.

Do not replace real owner tests with fakes. Verify actual scope resolution, read-only grants, removed-key refusal, independent preview release, safe native launch request and browser download bytes. Retain the P1 page/admitted mutation tests. Extend `ProjectsPortfolioBrowserTests`, and reuse the existing Agent/file and Workflow/TestLab journeys with a deterministic external model boundary; actual registered tools, approval, native writes and independent read-back remain real. No paid inference is authorized. Repeated unexplained timeouts stay visible; a later pass is not a causal repair.

Broad Stable is required only by a current named invalidation trigger or a deliberate final frozen checkpoint, not after each stage. Record the impact decision. If shared public contracts, file authority, core session semantics or common owners change beyond the bounded feature composition, use the appropriate broad gate once the source stabilizes. Required portability-static final enforcement is always independent of that decision. No baseline waiver, skipped-test relabeling, scanner weakening or removal of failure assertions.

Maintain a current Projects renderer census and documentation. After P2, classify remaining route/agent-context/effect hosts; do not impose zero Razor files as closure. Report separately: P1-R1 closed, Files P2 boundary complete, production journeys verified, current graph and publish proven, wider readiness/prerequisites still open. The inherited 40/40 live journal and historical selector uncertainty remain unchanged unless separately authorized/proven.

Stop only affected mutating paths for a genuine newly discovered critical problem. Repair small reproducible defects directly. If an unexpected fix requires changing multiple owner protocols, authority or schema, produce a concrete causal map, evidence and bounded next-bundle proposal instead of speculative redesign. Continue unrelated safe verification without claiming the blocked scope complete.

Deliver signed local commits only according to the existing repository instructions; preserve GPG configuration and the operator's unrelated work. Do not push/merge, change the normal application on port 5032, or bypass denied cleanup. Return precise code changes, paths, current source pair, actual test commands/results, supported-desktop screenshots, measured loop observations and remaining limitations. Do not start a third slice.
