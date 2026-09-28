# Execute: TestLab closure, then complete Plugins UI decoupling

You are implementing the next bounded UI extraction in CanDoItAll. Work as a pragmatic senior C#/.NET/Blazor engineer. Implement, test, document and finish the authorized slice; do not stop at an architecture proposal. The assignment is **one run: S0 TestLab closure first, then Plugins**. A small carry-over repair belongs in S0, not in another fixes-only handoff that postpones the next module.

## Starting point and authority

The source review used `fyziktom/CanDoItAll`, branch `components-decoupling`, commit `dd050d5a1489537207e073cac0838f40cde4340f`. This SHA records provenance, never an instruction to checkout, reset, merge or overwrite work. Inspect the actual branch, HEAD, worktree, relevant sibling revisions, SDK, dependency mode and running development processes. Preserve unrelated work and the user's normal application/database.

Read the current `AGENTS.md`, `.github/copilot-instructions.md`, `docs/architecture/ui-component-seams.md`, `docs/testing.md`, relevant `.github/workflows/ci.yml` jobs and repository-family standards in the available SharedInfo checkout. Read this complete package: [closure review](TESTLAB_CLOSURE_REVIEW.md), [Plugins analysis](PLUGINS_REVIEW_NOTES.md), [validation matrix](VALIDATION_MATRIX.md), [dev-loop proof](DEV_LOOP.md), and the [shared execution brief](shared/prompt.md) with its linked architecture documents. Do not execute historical module assignments found in `codex/bundles` as fresh work.

Use Code Analytics MCP for symbols, references and affected tests when available, and Components MCP for non-WebGL component contracts/recommendations as required by current instructions. Corroborate their output with source and actual discovery. If unavailable, explicitly record the limitation and use local search, evaluated build metadata and existing component contracts. Never invent an MCP result or weaken validation because a tool is unavailable.

Follow current source and canonical instructions where they supersede this review. Explain material differences in the implementation record. You may choose the smallest correct design; no quotas for interfaces, projects, classes, partial files, tests or commits.

## S0 — accept and, only if necessary, repair TestLab

Read `docs/architecture/testlab-ui-boundary.md`, especially the R1/R2/C1 corrective receipt, and inspect the current corrected code/tests. The reviewer found the prior issues addressed: independent reference/read-back waits; exact saved-party fallback; explicit Unknown notification title. See [closure review](TESTLAB_CLOSURE_REVIEW.md).

Confirm these behaviors through the current narrowly selected owning tests, with source-derived and verified discovery counts. Preserve the architecture, production session/owner and S0 conversation-shell fix. Do not reopen the entire extraction, replace its state machinery or expand into a general lifecycle framework.

If a genuine small regression is reproduced, repair it first with a failing-first test and bounded validation. If none is found, record S0 as accepted without a product edit and continue immediately to Plugins. A failed environment prerequisite is not a product bug: keep its proof status explicit and continue independent work, but do not claim full closure while required production proof is unavailable. A substantial safety-critical prerequisite must be documented honestly rather than bypassed.

## P1 — establish the Plugins baseline and seam

Read the full current `src/Modules/CanDoItAll.Modules.Plugins` rendered closure and its real owners. The review read the complete routed page and key children/helpers, but some owner files were only partially inspected; complete their current-source review before modifying them. Use [the source register](SOURCES.md) to distinguish fully read code, partial reads and inventory-only entries.

Preserve the entire current `/plugins` workspace:

- Catalog/tag tree, expansion state, plugin selection, manifests and availability, install/enable/disable actions.
- All six existing detail sections: Main info, Executors, Settings, Connections, Logs, Grants.
- Every supported schema field kind, connection name/enabled state, validation, OAuth status/start/disconnect and grant identity/scope.
- Separate bounded installation/runtime log streams and selected-plugin/all-plugin scope.
- Package catalog dialog, actual ZIP upload, install receipts and restart-required/requested state.

Preserve the current URL and callback contracts. The reviewed page has no plugin/tab query-state contract; do not invent a new deep-link protocol as a prerequisite. A typed internal section model is appropriate. Map it explicitly to the existing tab component's index rather than using a localized label or index as a durable identity.

Capture current affected tests and the before-move development-loop baseline. The existing `tests/Components/CanDoItAll.Tests.Components/PluginsPageTests.cs` exercises real owners; preserve its behavior rather than replacing it with sandbox-only tests.

## P2 — implement the real dependency cut

Suggested locations, subject to an equivalent seam already existing in the actual checkout:

```text
src/Modules/CanDoItAll.Modules.Plugins.Contracts
src/UI/CanDoItAll.Plugins.UI
src/Sandboxes/CanDoItAll.Plugins.UiSandbox
```

Keep the routed page and per-page session/adapter with the module. Keep EF, stores, plugin runtime/assembly loading, service composition, grant evaluation, vault, OAuth protocol handling, package installation and application restart with their existing owners. The production host may call them directly in process. Do not add HTTP between co-located components solely for decoupling. Preserve existing HTTP APIs and their authorization/serialization behavior.

Use a cohesive workspace-view contract, or a justified mixture of presentation records and typed intents. The renderer must not depend on the Plugins implementation, Infrastructure implementation, Security/vault implementation, workflow runtime, production Web/composition, concrete plugins or host tool execution. Descendants and dialogs count: a lightweight parent around heavy child renderers is not a completed cut.

Move only stable, backend-free model contracts needed across the boundary. Preserve their namespaces where practical, ID wrappers, enum numeric values, JSON names/defaults, manifest shapes and consumer semantics. Do not casually move or duplicate SDK/plugin-loader identity types. Add API/serialization compatibility tests for moved public types and rebuild real consumers.

`CanDoItAll.Plugins.Abstractions` already references `CanDoItAll.AgentFramework.Models` and SharedKernel. Models in turn references other abstractions. These are not automatically forbidden just because their names contain AgentFramework or Infrastructure. Evaluate their real transitive closure and public types. Reuse appropriate descriptive contracts instead of duplicating them to manufacture a smaller file count. If a narrowly scoped presentation projection is needed to avoid a heavy edge or sensitive payload, justify it. Do not restructure the entire plugin SDK in this slice.

Browser-local rendering/focus effects may remain with the renderer. Context-dependent callback URLs, permission decisions and OAuth/application effects belong to the host. Pass the existing callback URI as presentation data instead of having a settings child construct its parent's/API navigation context. Inspect and relocate icons, CSS, JS, imports and route/static-asset registration with the rendering responsibility.

## P3 — correct state, requests and mutation outcomes

Implement the concrete corrections in [Plugins review notes](PLUGINS_REVIEW_NOTES.md), with regressions first where feasible.

### Connection editor lifetime

Each editable connection has a stable, host-owned origin: plugin, descriptor/key, selected persisted connection identity, and draft generation. Preserve current cross-tab and cross-plugin unsaved-edit behavior; do not discard another connection's draft because one action refreshed the catalog. Define explicit reset/retirement behavior for a removed/replaced descriptor or connection. A newly observed connection with the same key is not automatically the old editor's target.

Capture text on input, including incomplete numeric/JSON/multiline values, before blur. Retain raw values and schema validation through section unmount/remount, status updates, reference reloads and a pending save. Existing editors use `ConfigurationState` and `ConfigurationValidationResult`, not an EditForm; do not add an EditContext merely to match TestLab. If a form/context is introduced for a genuine benefit, give it the same draft lifetime and prove it.

At submission, capture the entire operation input (ID, key, name, enabled flag, schema-filtered configuration and origin) independently of mutable live state. Admit one save per live editor operation; a retired editor must not block an independent successor. Keep fields editable while pending and reconcile newer input rather than disabling all editing to avoid the problem.

On success adopt the returned `PluginConnectionItem.Id` and appropriate accepted identity/version metadata **before any secondary read**. Reconcile unchanged fields against the submitted values, not the initial draft or current collection positions. Preserve later typing, other connections and validation. `IsDirty` must reflect newer unsaved edits; OAuth must not become enabled merely because a prior save completed.

A known save followed by a failed refresh stays saved with a warning and its committed ID. Retry refresh performs reads only. A genuinely unknown mutation retains its submission and prevents blind replay; provide an explicit operator recovery path. Never infer a create succeeded by matching its display name in a list. No durable idempotency or optimistic-concurrency protocol is required; current connection/grant `ConcurrencyToken` values do not by themselves imply owner enforcement.

### Independent reads and origin-bound effects

Use independent, explicitly owned lanes for catalog, selected settings/reference data, OAuth status, installation logs, runtime logs, packages and restart state as their semantics require. Coalesce or lazily load where useful; do not force every action to synchronously reload settings/OAuth for every plugin. Retain bounded log queries and test owner-call budgets rather than adding per-keystroke I/O.

Fence late success, failure and finally by actual request/origin, including A → B → A, selected/all log scope, a newer manual refresh, dialog close/reopen and disposal. Loading/unavailable/stale must not be represented as empty data or a definite disconnected/denied state. Initial default selection is allowed; a late read must not overwrite a newer explicit selection. A failed settings read must not make unrelated manifest or log information unusable.

Detach event handlers, cancel owned reads and observe asynchronous continuations. Own cancellation sources until their operations unwind; do not dispose a source still accessed by its continuation. After retirement, suppress stale UI notifications/popups/focus/reopen effects without pretending an admitted owner mutation was cancelled or rolled back. No unobserved task or global source cache shared across circuits.

### Grant and lifecycle admission

Fix the current `Granted`/`grant`, `Denied`/`deny`, `Revoked`/`revoke` busy-key mismatch with one typed action/target authority. A grant target includes plugin, capability, recipe, scope kind and scope key. Conflicting choices for the same target cannot run as independent writes merely because their action names differ; unrelated targets must not falsely collide. Handle enable/disable and connection/OAuth conflicts according to their actual shared target. Prove visible busy state and direct double-dispatch protection; disabled markup alone is not admission.

Preserve backend grant checks, declared capabilities, recipe requirements and stored-vs-evaluated scope behavior. Do not silently broaden privileges, invent API concurrency checks, introduce a new authentication system or change the current actor convention as part of UI extraction.

### Multi-stage owner results

Do not wrap all service exceptions or `Result.Failure` values as proof that no side effect happened. Inspect real commit boundaries: package extraction, installation persistence, restart metadata and logging are separate stages; OAuth start can create a connection before later validation and can persist a session before the browser effect.

Keep existing successful owner results and distinguish secondary failures. Where the current service hides a known completed stage, a small, typed owner receipt/exception or adapter extension is authorized, with owning integration tests and unchanged API compatibility where feasible. Do not create a general event bus, transaction coordinator or new plugin runtime. If an outcome truly cannot be determined, label it unknown and provide read-only review, not automatic retry.

Use safe diagnostic messages and correlation/target identities. Do not display arbitrary exception text, tokens, credentials, configuration payloads or full authorization URLs in general notices/logs/evidence.

## P4 — preserve real upload, OAuth and restart behavior

### Upload and package installation

Keep an actual InputFile-based upload in the shipped surface. A renderer may report an `IBrowserFile` through a UI-local intent or a similarly bounded browser-file seam; this is not a serializable domain DTO. The host owns opening, cancellation and disposal of the stream with `PluginPackageOptions.MaxPackageBytes`; the package owner retains its independent byte limits, temporary paths, validation, extraction and cleanup.

Specify and prove what happens when a second file is selected, the dialog closes/reopens, navigation changes, a read fails or the component is disposed. Browser file selection is not a durable handle. Either protect the input's reading lifetime or use a bounded host-owned staging design; never retain an invalid browser handle or hold the entire archive in a workspace byte array. A pre-admission cancellation is distinct from an already staged/installed package. Preserve committed package identity and restart-required state across refresh/log failures; never replay installation just to refresh the UI.

Keep file names untrusted, paths owner-generated, size enforced while reading, and existing archive/manifest/traversal limits intact. Sandbox upload uses harmless fixture bytes and a bounded fake operation, not the real installer, filesystem or assembly loader. Production proof uses test-owned paths and valid controlled fixture archives only.

### OAuth and restart

OAuth operates on the chosen persisted connection and its current clean validated settings, subject to real backend grants. The settings draft, effective permission and live status are distinct facts. Preserve disabled/dirty hints, start/reconnect/disconnect semantics and existing callback/return paths, state expiry, PKCE and vault handling.

The returned authorization URL is a short-lived host browser effect, not catalog state. Open only for the current authorized origin while alive, retaining the existing `_blank` and `noopener,noreferrer` behavior. Distinguish session creation, popup/browser failure and actual account connection. Do not start another session on a passive refresh, claim Connected after opening a tab, or reopen a retired dialog when a delayed call returns. Keep tokens/verifiers and protocol internals out of UI models, navigation state and evidence.

Restart is an explicit application effect owned by `PluginRuntimeRestartService`. A sandbox records the request without stopping itself. Test a real restart request only against an owned fixture process/lifetime, wait on its observable stop signal, and never restart or kill the user's app on port 5032. Do not preserve the current arbitrary 1500 ms test delay as the synchronization mechanism when updating that test.

## P5 — build the representative sandbox and prove production

Use the **same complete renderers** with actual BaseLib controls, tabs, dialog, tree, inputs and assets in production and the sandbox. Provide deterministic scenarios for all six sections, empty/large catalog, missing descriptors/references, unavailable/stale/partial reads, dirty/invalid fields, held reads/saves, known commit + failed refresh, true unknown result, permission restrictions, scoped grants, OAuth statuses/effects and package/upload/restart stages. Scenario transitions must retire their work; fake writes must update real fake storage before a later read can observe them.

Do not start a database, real plugin, provider/runtime, vault, OAuth network request or installer to render sandbox scenarios. A fake that only prints the requested success label does not prove identity, persistence or recovery. Test the fake's state semantics as well as its markup.

Execute [the validation matrix](VALIDATION_MATRIX.md). Reuse existing seven Plugins page behaviors, then add focused unit/session and lightweight component tests, real owner/API tests for affected contracts/effects, and Playwright on both the actual production `/plugins` route and sandbox. Use isolated PostgreSQL 18 and test-owned package directories. Replace only the external boundary being controlled; do not replace every owner in a test claiming real persistence.

Derive case counts from current source and match `--list-tests` before each new/changed filter. Rebuild changed production projects and their affected consumers directly. New test projects must be in the relevant test solutions/CI selection, not the product solution. Run required portability-static, reviewed baseline reconciliation, documentation/evidence and whitespace gates. Widen to Stable/platform/owner lanes only for real current invalidation triggers; an arbitrary phase end is not a full-suite trigger.

Evaluate sandbox/project/package/public-type/static-asset closure, including sibling source mode. Boundary guards reject forbidden transitive and unexplained unresolved edges. Measure the actual before/after development loop as specified in [DEV_LOOP.md](DEV_LOOP.md). Never disable required watch inputs or use stale binaries to manufacture speedup.

## Completion, commits and scope limits

Keep implementation, UI, code comments, tests and maintained documentation in English. The owner-facing final summary may be Czech. Update the module boundary record, local READMEs, UI/sandbox indexes and testing guidance; avoid another competing shared rulebook. Record S0 outcome, architecture, contracts/consumer consequences, resolved defects, remaining debt, exact commands/counts/results, assets, measured samples and cleanup.

Signed local commits on the existing authorized branch are permitted. Preserve the normal unlocked GPG agent/session for later commits; do not disable signing, store passphrases or change global trust/cache settings to bypass a prompt. Do not push, merge, publish, reset or alter sibling repositories without separate authorization. Keep the user's work intact.

Do not start SchedulerPlanner, Processes, Workbench or another module in this run. Do not add a new scheduler, plugin execution model, HTTP-only UI, schema migration, token protocol, generic governance framework or blanket full-application performance refactor. Necessary narrow owner/result fixes and directly affected consumer/test updates are in scope. Deliver a functioning complete Plugins seam and sandbox, with honest validation status, not a catalog-only prototype or another TestLab-only completion.
