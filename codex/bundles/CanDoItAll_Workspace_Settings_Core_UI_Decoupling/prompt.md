# Codex GPT-6 Astra Max — Resources closure + Workspace Settings Core

## Mission and execution contract

Implement this assignment on the user's current CanDoItAll checkout. First reproduce and
repair **RS-R1**, the Resources exact-editor readiness defect. Then implement **Workspace
Settings Core**: the Settings shell and the complete Workspace defaults, Secrets, Files and
Provider history sections, with actual production integration and a backend-free sandbox.
Do not stop after the Resources repair. Do not extract a third area or claim that all Workspace
UI is complete.

Read this entire package, not only this prompt. Start with [the review](RESOURCES_REVIEW.md),
[scope selection](MODULE_SELECTION.md), [Workspace source notes](WORKSPACE_REVIEW_NOTES.md),
[sensitive-state rules](SENSITIVE_STATE.md), [validation](VALIDATION_MATRIX.md) and
[development-loop proof](DEV_LOOP.md). Read the bundled [shared foundation](shared/README.md)
and its architecture/validation guidance. [Sources](SOURCES.md) distinguish full reads,
partial reads, discovery and implementer-reported evidence.

The review examined `components-decoupling` at
`20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d`. Its Memory prerequisite is
`9dac16414e38774eb7eb96d22cde7480bb31f9ea`; the preceding handoff archive is not product code.
These are provenance, **not execution pins**. Do not checkout/reset/revert to these commits,
recreate existing Resources projects, or execute an archived prompt as a fresh assignment.
Read intervening changes and adapt to the actual working tree without overwriting user work.

Before changes, read current `AGENTS.md`, `.github/copilot-instructions.md`,
`docs/architecture/ui-component-seams.md`, `docs/testing.md` and `.github/workflows/ci.yml`.
Use available current CanDoItAll.SharedInfo standards/skills. Current repository policy takes
precedence over historical examples in this package. Use Code Analytics and Components MCP
when available and appropriate; otherwise inspect actual source/contracts and record the
fallback. Never invent tool availability, discovery counts or measurement results.

This is an in-process UI/build-graph extraction, not an HTTP-only rewrite. Keep the production
owners, governance and persistence semantics. Choose cohesive seams and ordinary composition;
there is no interface/project count quota and no mandatory general-purpose controller framework.
All new source comments, repository documentation, UI text and commit messages are English.
A final conversational summary may be Czech.

## S0 — finish the bounded Resources correction

The current `ResourceRegistryController.RefreshAsync` can set `Access = Ready` after a
successful catalog/reference read while `LoadEditorAsync` is pending, or after that exact
editor read failed. `SelectAsync` then treats that ID as an already loaded no-op. The route
also maps this aggregate Access to Agent context readiness. See RS01, RS03, RS07 and RS09.

Create failing-first deterministic tests for both orderings:

1. Select an existing B from A, hold B's exact owner editor read, and complete a header
   reference Refresh. Catalog success must not claim the B editor is acquired or admit a
   mutation against its placeholder. The actual Agent context must remain non-ready.
2. Fail the exact read, then succeed at reference Refresh and select B again. B must be
   retried or have an explicit usable exact-editor retry; the same-ID optimization must not
   strand a placeholder. Cover initial route failure/recovery as well as ordinary selection.

Separate catalog/reference availability from exact-editor acquisition and its origin. A
reference refresh must not complete, clear or resurrect another editor request. Gate direct
Save/Delete admission on a genuinely acquired or deliberately new editor, not on a generic
boolean whose meaning changed. An already loaded historical resource can still be cleaned up
according to existing owner rules even when optional references are stale; do not replace the
bug with a blanket requirement that every reference read succeed before every mutation.

Preserve user text, `EditContext`, configuration/raw validation, project lifetime, confirmed
IDs and all existing outcome fences. Repeated selection of a successfully acquired same ID
remains a no-op. Test A-B-A, superseded failures, disposal, missing IDs and reference failures.
Use the actual top-level renderer controls and a production-host Agent context regression in
addition to controller calls. No sleeps, disabled-button-only proof, backend authorization
relaxation, route protocol redesign, or file browser rewrite is needed.

Recheck the existing Memory result-origin repair with its targeted tests. It is accepted
source direction, not a request for another Memory extraction. Preserve Scheduler and Plugins
repairs. Finish S0 and continue into the selected Workspace scope in the same assignment.

## W1 — inventory the selected Settings boundary before moving it

The current shell exposes eight navigation items. Preserve their public tokens and behavior:
`workspace`, `data-sources`, `storage`, `files`, `provider-history`, `secrets`, `providers`,
`api-access`. Providers redirects to `/agents?tab=providers` with existing replacement behavior.
The other seven are local sections. Missing/invalid `tab`, direct links, back/forward and
same-section rerenders must remain coherent. Do not create a new URL/query protocol.

Extract these complete workspaces:

- Workspace defaults: name, provider reference, output format, currency code/culture and notes.
- Secrets: metadata list/filter, explicit load for editing, new/save/clear/delete, actual
  SecretField masking/reveal/copy behavior, kind/scope/rotation note/metadata JSON.
- Files: machine-local preferred-application override list/editor, save, select, new and
  restore system default, with original path validation and host-bound state.
- Provider history: explicit policy Load, editable policy fields and validation, future-only
  update, bounded shorter-retention preview and separate confirmation dialog.

**Defer** the implementation extraction of Data Sources, Storage (including recovery/pickers)
and API token/user/access administration. Keep their real production hosts and commands
working through explicitly composed active-section slots/host adapters. The light rendering
project must not reference those implementations. No placeholder may replace them in the
production app. Their sandbox entries may be visibly outside scope; never portray them as
working extracted screens. Do not pull the whole security/runtime/database module into the
sandbox to make its navigation identical.

Read full selected source, their current consumer tests and owner contracts. Map source and
runtime dependencies, render tree, static assets, DI/route discovery, public/wire contracts,
permissions and origin lifetimes. Capture a baseline after S0 using isolated synthetic data.
Inventory the deferred section hosts only enough to preserve integration; document that they
are not extracted. Classify touched tests using current source rather than a historic namespace
filter or file-count rule.

## W2 — establish a real lightweight seam

Suggested locations (reuse equivalent current seams if present):

```text
src/Modules/CanDoItAll.Modules.Workspace.Contracts
src/UI/CanDoItAll.Workspace.UI
src/Sandboxes/CanDoItAll.Workspace.UiSandbox
```

A `Workspace.Presentation` project is appropriate only for cohesive state/operation behavior
used by both production and sandbox. The route remains a host owning navigation, production
composition, authentication/profile notifications, effects and sensitive-state retirement.
Children own their real rendering, element references, focus and transient disclosure state.
Do not replace one giant codebehind with a giant service bag, partial-class cluster, generic
bus or two divergent production/sandbox controllers.

Move or project the actual data-only values needed across the seam. `WorkspaceSettingsModel`
currently shares a file with EF entities and `WorkspaceService`. `WorkspaceProviderOption`
and its catalog port are already small. Keep EF/configuration/persistence out of the light
contract graph. Preserve existing enum values, namespaces where useful, defaults, serialized
shape, API descriptions and concrete public consumers; identify actual binary requirements
rather than adding speculative compatibility machinery.

Secrets are owned by Security, not Workspace. Reuse an appropriate existing light security
contract or use reference/editor projections at the UI boundary. A narrow Security contracts
extraction is possible when justified, but do not introduce Security-to-Workspace implementation
edges or relocate the vault/protection/runtime resolver into UI. An ID/name picker type must
not become a secret-value DTO by convenience. If an editor DTO can contain plaintext, it is
transient sensitive state, not a serializable navigation or receipt type.

Reuse the existing `ProviderHistory.Abstractions` policy port and immutable records. Do not
copy the policy protocol or move its persistence implementation. File preference values can
be projected at a narrow owner port; referencing `Infrastructure` simply to name a path or
extension is not a valid light dependency. Keep application execution, durable file writing,
path migration and rollback in their existing owner.

Reuse BaseLib and the existing Configuration.UI child where it actually applies. Do not copy
component library implementations or introduce Radzen/new CSS frameworks. Move scoped styles,
imports/static assets/asset registrations with their renderers. Explicitly evaluate sibling
source replacement in `Directory.Build.targets`; package references alone are not graph proof.

## W3 — state, origin and outcomes

### Independent lifetimes, not a universal settings snapshot

Defaults and Secrets belong to the canonical database profile. Files belongs to the local
host control plane. Provider history additionally depends on the caller's management authority,
partition and expected policy version. Model these separately. A profile/authentication change
must invalidate relevant reads and prevent a delayed command from resolving a new profile at
dispatch. Preserve known effects on their original owner; navigation does not prove rollback.
Do not unnecessarily reload or discard unrelated machine-local file preferences.

Capture all submitted fields before the first incomplete await. Preserve nested values,
configuration and user edit-away-and-back. A route/section switch, explicit record switch,
new/reset, same-record refresh and disposal are distinct transitions. Refresh must not replace
an active draft or reset raw parsing/validation/focus. Missing saved references stay visible as
unavailable; never choose the first provider/secret/extension as a fallback for an explicit ID.
Initial loading must not overwrite input typed while it was pending.

Use actual input events before blur for live text, and stable form ownership. Keep invalid
numeric/policy input editable rather than silently clamping, dropping or validating an older
model. Admission must exist in handlers as well as disabled controls. Enter and click, Save
and Delete, and multiple callbacks for one origin must not produce conflicting writes. Let
independent targets proceed when policy permits; do not lock the entire Settings page merely
to avoid correct lifetime handling. The history policy panel's existing deliberate editing
lock during its own operation may remain.

Preserve four result categories: refused before the operation, observed successful result,
known persisted result with a later warning, genuinely unknown result. Keep receipts bounded
without evicting unresolved operations to allow replay. A successful owner result followed by
failed list/provider reload is a read-back warning, not an unsuccessful write. Recovery reads
are single-flight/origin-fenced, do not replay the operation and cannot alter a successor's
Pending state. Read-only observation of a current row must not claim which request created it.

### Workspace defaults

Capture the six settings fields before owner waits. Apply returned normalized fields only
where the user has not made a newer edit; retain newer raw text and form context. Preserve the
owner's latest-record selection, currency normalization and default provider reference meaning.
Read provider options independently; failed provider-list refresh after a save must not cause
the defaults to be resubmitted. Missing/disabled stored providers must not silently change.

The current owner updates CurrencyDisplayState before durable SaveChanges, and its read path
also publishes normalized display settings. Inspect that ordering and add a bounded regression
for failed persistence. Prevent an unsuccessful write from advertising uncommitted settings;
preserve existing deliberate read normalization without inventing a new global event system.
Do not change currency defaults or database schema as a by-product.

The HTTP workspace-settings endpoint has intentionally separate validation, capability scopes,
six-field replacement, unknown-member rejection and a saved-snapshot/pending-read-back header.
Preserve these wire/transport semantics and test actual consumers of any moved model or owner
outcome. Do not expose deployment switches or authentication configuration through this form.

### Secrets

Read [SENSITIVE_STATE.md](SENSITIVE_STATE.md) before implementation. Lists and page statistics
are metadata-only. Resolve plaintext only for explicit secret editing through the existing
Security owner, never as a catalog prefetch, hidden section initialization or sandbox dependency.
Use the actual SecretField; preserve safe copy/reveal timers, and retire a revealed renderer
when the section/record loses its disclosure lifetime. Preserve intentionally retained masked
editor text without persisting it or putting it in a hidden revealed input.

Capture a private per-operation command before awaits; retain it only as long as necessary.
No plaintext, full command/editor, vault key, encrypted payload or arbitrary metadata JSON in
mutation history, logs, URLs, browser evidence or generic serialized state. Redacted receipts
may retain exact record ID, action, origin, stage and safe diagnostic code. Do not promise
cryptographic erasure of immutable .NET strings; remove unnecessary references and dispose
owned components/subscriptions.

Adopt the owner-returned secret ID before refreshing the metadata list. A completed Save/Delete
must not reset a newer selected secret or draft. On ordinary successful create with no newer
input, retain the current clear/new behavior safely. Preserve newer fields during a delayed
update; a retired operation retains only redacted facts. Explicit missing-secret load is not a
new record and cannot accidentally resurrect the missing row via the owner's existing upsert
behavior. Preserve that owner's non-UI contracts rather than silently changing all callers.

The existing owner stages a new vault value, commits metadata, removes old payload, records
Activity and returns. Deletion commits protected metadata removal before deleting the payload
and recording Activity. Preserve the exact successful durable stage and ID across secondary
failure with a bounded owner result/observation if needed. A postcommit cleanup warning must
not invite blind resubmission or claim the payload was removed when it was not. A real commit
acknowledgement failure remains unknown; do not unconditionally delete a possibly referenced
payload to make a test green. Inspect real owner/transaction helpers before changing cleanup.
No vault migration, new cryptography or distributed-transaction framework is authorized.

Keep deletion reference policies, stable locking, transaction participation, legacy protection
handling and current resolver behavior intact. Known validation/reference denial must remain
correctable and distinguishable from unknown. Widen targeted proof to real Security consumers
when touching the owner, not just a fake UI receipt.

### Files

Capture the exact normalized extension, executable path and origin for Save/Delete. A newer
selection, edit or reset while persistence/list refresh is pending must not inherit the earlier
extension or be cleared by its completion. Preserve unfinished raw input and usable stale lists
on read failure. Update the original operation receipt, not the currently selected editor.

Keep machine-local host binding and `RequiresRebind`/inactive states; do not present an imported
inactive path as an active override. Use the current executable/path validation and durable
writer. Distinguish successful durable write from logging/read-back failure. Test in a private
control-plane directory with a harmless existing fixture path; never execute it as proof.
No command templates, shell fragments, process-launch capability or OS association rewrite.
Existing lazy legacy migration behavior of the owner is not a new UI feature; inspect and
preserve it. Migration/rollback workflows remain outside this slice.

### Provider history

Reuse current behavior: opening the tab causes **zero policy/history reads**. Load is explicit
and permission-checked. Preserve `HistoryPolicySnapshot.Version`, `ExpectedVersion`, conflict
handling, Manage permission, profile partition/write fence and the owner's atomic policy/audit
update. Never turn the trusted local UI into a bypass for denied/stale context.

Future-only update and shortening existing expiry are different explicit commands. The latter
requires a preview bound to the exact draft/policy version and separate confirmation. Invalid
raw input, stale preview, auth/profile retirement or ExceedsLimit must prevent confirmation.
The server remains authoritative and rechecks bounds/version. Preserve quotas/limits and the
warning about sensitive Detailed capture. Do not delete canonical conversations, extend old
expiry, reconstruct prompts, start workers or add a history content viewer in this task.

Failed/unknown updates keep safe captured facts and cannot blindly replay destructive work.
Reload observes the current policy; it is not proof of the earlier update. Retire policy and
preview state on auth/profile change without an automatic new-policy read. Prove event and CTS
ownership, including late continuations and disposal.

## W4 — production shell and genuine scenario host

The route owns token parsing/navigation; pure renderers raise typed section intents. Preserve
all current routes and the Providers redirect. Host deferred Data Sources/Storage/API Access
through real active-section composition. An unselected slot must not eagerly initialize its
backend or fetch tokens/users. Keep those original commands and security checks unchanged.
Do not display API authorization as definitively disabled merely because its status is still
unloaded or unavailable.

Make the four extracted sections share the exact shipped renderer/presentation logic with a
lightweight scenario store. Scenarios must mutate their own state and support controlled
waits/faults: initial/empty/populated, missing provider or secret, partial reference errors,
pending save/read-back, unknown and exact review, newer edits/selection/reset, auth/profile
retirement, file rebind states, invalid policy input, bounded preview/confirmation and refusal.
Use fixed identities/time where relevant and bounded catalogs/receipts. Delayed admitted writes
update their original store after sandbox reset without publishing into the new workspace.

A synthetic secret may be revealed only through the same real SecretField; it is not evidence
of a production vault. The sandbox does not require a real vault, database, driver, API signer,
provider account, key ring or hosted worker. Keep test-only controls out of production UI/DI.
Explicitly label the other three local sections as not part of this sandbox proof.

Serve actual BaseLib/theme/font/scoped assets. Link generated application CSS as explicit content
when following parity mode, not as a Web ProjectReference. Verify both source and published
Production-environment standalone hosts. Record unsupported/hot-reload-restart cases honestly.

## W5 — validate, document and hand off completed work

Use [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md) as obligations, not a rigid order of commits.
Start with failing-first targeted tests and build-backed discovery. Record expected and actual
discovery/execution counts; explain legitimate MemberData expansion. Do not count source-method
counts, an empty filter, skip, quarantine or missing prerequisite as a passing run.

Build each affected production root directly. Prove real controls, actual hosted route and
unmodified deferred sections, owner semantics on isolated PostgreSQL/vault/control-plane,
policy authorization/retention and exact secret identity/cleanup. Include tests for existing
HTTP settings contract and affected Security/FileTools/History consumers. Browser tests must
wait for the particular operation's observable result, not a button's existence or disabled
state. Do not introduce fixed delays to conceal races.

Use current Code Analytics to identify impact if available; otherwise record source-based
selection. Broad Stable/platform suites follow current named invalidation triggers, not every
minor phase. A root policy, common fixture, shared lifecycle or security-owner protocol change
must widen proof appropriately. Never weaken shared tests merely to simplify extraction.

Run mandatory portability-static on the complete proposed tree, review added/stale findings,
fix actual defects, inspect any intentional baseline update, then enforce again without
`--write-baseline`. Run maintained documentation/evidence and secret-scanning gates. Test and
review the screenshot contents; synthetic plaintext used for assertions need not be copied
into retained evidence. Treat package utilities as package proof only.

Measure the development loop as specified in [DEV_LOOP.md](DEV_LOOP.md). Report actual evaluated
project/package/native and watch graphs. Full Web remains broad; do not infer its acceleration
from a small sandbox or compare unmatched runs as a statistical benchmark.

Update maintained architecture documentation with the **partial Workspace completion map**,
new project READMEs, direct sandbox commands, current test commands and actual evidence. Archive
input bundles only as history; don't mutate the sealed shared foundation or turn old counts
into new acceptance proof. Update real product/test solution and CI membership according to
current conventions; no test project belongs to the product solution.

Use task-owned processes, ports, PostgreSQL 18, temporary directories and private test vaults.
Never reuse or restart the ordinary application/database on port 5032 or kill processes by name.
Do not stop unrelated Docker resources or alter sibling revisions/user local MCP settings.
Use an isolated build configuration such as `WorkspaceSettingsUiProof`. Record and clean only
owned resources, checking identity before shutdown. Preserve user working changes.

Locally signed commits are allowed when consistent with current user/repository policy. Keep
GPG signing enabled and use the existing signing session; do not rewrite Git config, reset
history, force push, merge, release or deploy. No remote push is requested by this handoff.

The final implementation report must state initial/final SHA, scope, RS-R1 reproduction/fix,
actual contract/owner changes, four-section and deferred-host status, exact validation and
measurement receipts, sensitive-state checks, cleanup and remaining limitations. Completion
means S0 plus the four-section extraction, sandbox and real production proof—not just project
creation, screenshots of mocks or a plan for future testing. Report real blocked gates honestly
without falsely describing unfinished proof as green.
