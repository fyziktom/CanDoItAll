# Plugins source review and architecture

Review SHA: `dd050d5a1489537207e073cac0838f40cde4340f`. The full current routed page, key renderers/helpers, public model/project files and the owning component journeys were read; owner files explicitly marked partial in [SOURCES.md](SOURCES.md) require completion during implementation.

## Why this module next

Plugins is a bounded medium-sized administration workspace, already divided into identifiable tab components. It offers a complete user journey and a clear seam without requiring the large Processes/Workbench runtime surfaces. It is not a trivial rename: its async editors, grant decisions, browser-file lifetime and multi-stage installation effects need deliberate ownership.

The current module still compiles its rendering alongside EF, Infrastructure, Security and workflow/plugin runtime references [P04](SOURCES.md). Merely moving the Razor page into a wrapper or replacing its injected concrete services with one interface while keeping the same assembly graph would not meet the goal.

The complete existing UI boundary is [P01–P03, P05](SOURCES.md):

| Region | Existing behavior | Ownership after extraction |
| --- | --- | --- |
| Header/catalog | Counts, grouped/tag tree, selection, expansion, manifests, availability and lifecycle buttons | Real UI in RCL; source loading/selection/admission in page session |
| Main info | Metadata and manifest details | Render descriptive contracts; no plugin loading |
| Executors | Descriptor-provided executor/policy/settings metadata | Render metadata, never execute an executor to display it |
| Settings | Schema fields, connection name/enabled state, validation and save | RCL inputs; host-held independent editor drafts/submissions |
| Connections | Saved connection and OAuth status/start/disconnect, dirty/permission hints | RCL display/intents; actual OAuth and browser effect in host |
| Logs | Installation/runtime streams, selected/all plugin scope, bounded queries | Independent query lanes and explicit stale/failure states |
| Grants | Capability/recipe/scoped grant rows and decisions | Typed full-target intents; owner policy stays authoritative |
| Package dialog | Catalog installation, actual ZIP InputFile, status and close | Real dialog/selection; stream/install lifetime in host/owner |
| Restart | Required reason and explicit requested state/action | Host owner effect; harmless scenario action in sandbox |

There are six current detail sections. `PluginsPage.razor` does not currently expose query parameters for plugin/tab selection. Preserve existing routes and OAuth callback/return URLs; do not make a new navigation protocol a prerequisite.

## Concrete source findings

These are code-derived findings, not runtime reproductions by the reviewer. Add deterministic regressions before the corresponding repair. The labels below are local to this assignment.

### P1 — global refresh destroys unrelated/current connection drafts

`LoadAsync` finishes with `InitializeConnectionEditors`. That method assigns a new `PluginConnectionEditorState.Create(...)` to every valid key regardless of `IsDirty`. `RunBusyActionAsync` calls `LoadAsync` after every action. Therefore a manual refresh, grant/lifecycle action, OAuth action or one connection save can replace another connection's unsaved editor. Newer text typed while a save is awaiting also loses its origin during successful reload [P02/P03/P07](SOURCES.md).

The settings fields use `onchange`, so text that has not blurred is not yet in the draft. On top of the editor replacement problem, a rerender/section transition can erase input not captured by the model [P06](SOURCES.md).

Required resolution: stable per-connection editing origins, raw input capture, explicit reset/retirement and narrow reconciliation. Current switching between plugin entries retains the dictionary of edits until reload; do not replace this with silent discard-on-selection. Keep clean editor caches bounded, but never evict a dirty/pending draft without an explicit documented user transition.

### P2 — known successful save can become a failed-looking duplicate create

`SaveConnectionSettingsAsync` passes the editor ID to `PluginSettings.SaveConnectionAsync` but only consumes the Result for a status message. The returned connection ID is not assigned to the original editor, whose `ConnectionId` is currently get-only. The common action wrapper then reloads all data. If this reload throws, the catch reports failure and removes the busy entry while the original new editor still has a null ID [P02/P03/P07](SOURCES.md).

`PluginConnectionStore.SaveAsync` commits and returns a `PluginConnectionItem`; a subsequent request with null ID creates a new Guid [P12](SOURCES.md). This establishes a concrete duplicate-create path after a known successful first write and failed refresh. No assumption about unknown distributed outcomes is needed for this path.

Required resolution: adopt the actual returned identity/accepted metadata before any later read, snapshot all submitted fields, and merge accepted unchanged values while retaining newer edits. Refresh failure is a postcommit warning. Retry refresh must not call Save. A separate genuinely unknown result must remain distinct and cannot be resolved by an old success receipt or display-name matching.

### P3 — stale reads and late effects can cross selections

The page has no request generations or disposal boundary. `LoadAsync` captures the previous selection and writes it back after awaits. `LoadPluginLogsAsync` captures the selected/all filter, then assigns installation and runtime results independently without checking whether the selection/scope is still current. A slow A read can therefore display under B, including A → B → A. Broad action completion can overwrite a newer notice and trigger a reload/pop-up on a retired page [P02/P03](SOURCES.md).

Required resolution: independent, source-bound reads with explicit loading/stale/unavailable state and a page-owned lifetime. A result for one selection cannot be shown as another's empty/success state. The catalog may choose a default on first load, but a stale captured previous selection must not win over a later user action. Scope-specific cached data must carry its source identity.

### P4 — grant busy identity disagrees with the buttons and omits scope

`UpdateGrantAsync` calls `GrantBusyKey(grant, state.ToString())`, producing `Granted`, `Denied` or `Revoked`. The buttons use `grant`, `deny` and `revoke`; case-insensitive comparison does not reconcile those different words. Their busy indicators therefore do not match the running handler [P02/P08/P09](SOURCES.md).

The helper key includes the action, capability and recipe, but not `ScopeKind`/`ScopeKey`. Different grant scopes can falsely collide, while conflicting decisions of one grant use different keys and are not mutually excluded. Required resolution: one typed target/operation authority shared by admission and rendering, with full grant identity. Test one same-target write, truthful visible busy state and independent other targets. Do not add owner-side optimistic concurrency just because the DTO contains a token: the current public contract states it is not enforced [P11](SOURCES.md).

## Dependency and contract design

Recommended relationship:

```text
Production module page/session/adapter --> Plugins.UI --> Plugins.Contracts
        |                                     |                 |
        +--> existing owners                  +--> BaseLib      +--> justified existing abstractions

Plugins.UiSandbox --> same Plugins.UI + bounded scenario state/store
```

The diagram is a responsibility model, not a demand for one interface or project per arrow. Reuse typed IDs, `ConfigurationState`/schema types and descriptive contracts when their actual closure is light. Moving stable models out of the implementation assembly is useful; copying every SDK descriptor into parallel DTOs is not automatically useful.

`Plugins.Abstractions` references AgentFramework.Models and SharedKernel [P15]. Models references Capabilities, Memory, Infrastructure and ProviderHistory **abstractions** [P16]. These names alone do not prove a heavy runtime dependency. Evaluate source-mode project references, packages/native assets and actual public-type closures. Permit justified descriptive abstractions, but reject implementation hosting, EF, vault, actual plugin instances/loaders and workflow execution runtime.

Likely movable module contracts include catalog DTOs/enums, grants/connections/settings, bounded log items, package catalog/results/restart status and OAuth status. Inspect the whole source files before moving them: names such as Models do not certify every contained type belongs in a light project. Preserve external JSON/default/enum/ID behavior. Keep protocol secrets, host services/options, executable delegates and dynamic plugin-loading identities out of new shared contracts.

The settings child currently injects NavigationManager to calculate the OAuth callback placeholder. Move this context-dependent calculation to the host and pass its result. Other renderer-local UI operations need not be forced into application services. There is no existing EditForm contract to preserve here: retain ConfigurationState/validation lifetime without imposing a form abstraction quota.

## Owner effects and safeguards

### Persistence and grants

Keep installation, settings, grant evaluation and log stores in their current module. Backend grants are not replaced by UI flags. Current APIs store connection SettingsJson without schema validation and return it unredacted; OAuth tokens are separately owned. Preserve wire behavior and do not introduce secret fields, log settings payloads or claim that existing UI validation is server authorization. A broader settings-validation/security redesign is not part of this extraction [P11–P13](SOURCES.md).

Preserve exact grant scope/recipe identities, actual supported enforcement and actor conventions. Stored Connection/Workflow scopes must not silently gain semantics in the refactor. Conflicting UI actions need local admission without pretending to solve multi-client database concurrency.

### Package installation and restart

The real package owner stages bounded uploads under an owner-generated temporary path and deletes that upload file in finally. Installation performs manifest handling/extraction, database installation, restart metadata and logging in separate stages [P14, partial read]. Complete the owner review and identify the real durable/partial results before classifying failures. A known installed package plus failed refresh/logging is not safe to replay as a new install. A typed bounded result at the real owner boundary is justified when necessary; a new generic saga or transaction layer is not.

Keep configured limits and existing archive validation intact. Upload must remain a real shipped input, not be dropped from the extraction to simplify tests. Dialog and selected-file lifetime are part of the seam. Framework guidance on replacement of file selections, explicit stream bounds and avoiding whole-file memory buffering is in [W01](SOURCES.md). The design must make a second selection or close during read deterministic, with cleanup and honest admission/result status.

Restart tests operate exclusively on test-owned process/lifetime. The sandbox records restart state without stopping the user's app. The current component test contains a fixed 1500 ms wait; replace that synchronization with a bounded observable lifetime signal when migrating it [P17](SOURCES.md).

### OAuth

The current owner verifies descriptors and grants, resolves or creates a connection, validates client ID, creates PKCE/vault state and persists a pending session before returning the authorization URL [P18, partial read]. A failure is not necessarily a no-side-effect result: even an early validation refusal can follow connection creation. Inspect subsequent callback/token/disconnect paths and preserve them rather than rebuilding the protocol in the UI.

The host opens the returned URL only for the appropriate current intent. A created authorization session, an opened tab and an authenticated account are different outcomes. Passive refresh does not begin a new session. Dirty settings stay unsaved until an explicit save; do not silently use a different connection because its key/display name matches. Keep protocol material out of generic state, logs and evidence; preserve stored-token/vault ownership and existing callback semantics.

### Logs and availability

Maintain the two bounded streams, per-plugin/all scope and sanitized store semantics. Log details may be truncated text rather than parseable JSON [P11]; render safely as data. Read failures should not erase accepted logs or impersonate successful zero results. No automatic unbounded polling or all-plugin reload on every edit.

## Existing test anchor and change surface

`PluginsPageTests` contains seven inspected Fact journeys before its helper section [P17]. Preserve their intent: descriptor executors, tag grouping, no-executor empty state, saved settings read-back, real owner OAuth URL/new-tab behavior, package installation/restart and selected-plugin logs. The body of the file was read through line 270; helper implementation beyond that range was not reviewed in this pass.

Additional likely affected consumers must be discovered in the actual checkout: Plugins API mapping/contract tests, manifest/SDK serialization, package staging/restart, OAuth/grants, workflow executor adapters, module composition, route discovery and any existing shared icon/configuration renderers. Do not invent missing test names or claim all seven tests passed merely because their source was read.

Keep changes localized to feature contracts/UI/session, sandbox, owner outcome adaptations actually needed, directly affected consumers and proof. No migration, unrelated module extraction, global service registration rewrite or SDK version upgrade is required.
