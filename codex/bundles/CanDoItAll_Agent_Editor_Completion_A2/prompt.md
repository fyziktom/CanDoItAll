# Execute: complete the technical Agent editor after A1

You are the senior C#/.NET/Blazor implementer. Deliver working source, real production composition,
faithful scenario coverage, tests and maintained documentation. This is a larger bounded
continuation, not a request to plan only. First fix S0, then finish all A2 sections in the stages
below. Do not stop after one section or start another module.

## 1. Establish the real source and preserve history

Read current `AGENTS.md`, `.github/copilot-instructions.md`, `docs/testing.md`,
`.github/workflows/ci.yml`, `docs/architecture/ui-component-seams.md`, applicable local guidance and
available CanDoItAll.SharedInfo standards before edits. Read [shared/README.md](shared/README.md)
and all required documents in `bundle.json`. The shared foundation is unchanged; this package's
current review takes precedence over its historical module census, not over repository policy.

The reviewed implementation was `ed64d4edf868cb26c749c31a94ff918683e0f4a0` on
`components-decoupling`. The subsequent `0aad5360b4ac037ed4471ed44fc6083b7f09fb85` is the operator's
**archive-only A1 bundle commit**. Do not execute A1 again or mistake that last commit for the
product diff. Record actual HEAD, tree and dirty state; work on the existing checkout, not a
reset to these SHAs. Components development was the already-published
`4a858412d2c2a3f6123bf23d8c4584f05b47627d`; record actual sibling/package resolution before use.
Publication is not proof of which binary or static asset the host loads.

Historical handoffs are intentionally retained during this refactoring wave. Leave them intact.
Do not delete or rewrite old archives, sealed manifests, working evidence or unrelated untracked
files to make Git look clean. Do not commit unrelated work. Scoped signed commits are permitted
under current repository/operator rules; retain the normal GPG agent/session without weakening
signing, exporting keys or changing global configuration. Do not merge, push, publish or open a
PR. Those are operator actions, not completion work for this prompt.

Use Code Analytics, Components and dotnetwatch MCPs if available. Inspect selected real widget
contracts/examples. If unavailable, record once and use exact source, evaluated MSBuild,
bounded caller searches and CLI testing. Do not burn the session on repeated unavailable calls
or write a new general analytics system.

## 2. S0: verification is not a save of the editor

Read [S0_VERIFICATION_RECONCILIATION.md](S0_VERIFICATION_RECONCILIATION.md).
Reproduce **A2-R1** against the current implementation before repairing it:

1. Open a persisted agent with a verifiable assigned capability.
2. Change its name/instructions and a reference/permission in the live editor, but do not Save.
3. Invoke Verify from the actual capability list. Make the native diagnostic complete safely.
4. Let the follow-up read finish, without additional typing after the Verify click.
5. Assert that all pre-existing unsaved edits, their EditContext, raw validation and section survive,
   while persisted unrelated agent fields remain unchanged and the real proof/version is updated.

The current host captures the unsaved draft, calls a verification method accepting IDs only,
then reuses save reconciliation. `HasLaterEdits` compares with the moment Verify was clicked;
when nothing changed afterwards, `owner.Load(refreshed.Draft)` discards edits made before Verify.
This is a source-derived inherited defect, not an independently executed reviewer test or an
assertion that A1 introduced it.

Repair by separating verification read-back from save acknowledgement. Do not work around it by
implicitly saving unrelated edits, disabling Verify whenever the form is dirty, reverting the
persisted diagnostic, or refreshing/remounting the whole form. Capability proof publication
also changes the agent's native update version: handle that fact explicitly, but do not simply
adopt the newest version and thereby mask another user's configuration change.

Reuse the existing diagnostic outcome/receipt and native ownership concepts. An exact own-proof
read-back may advance the concurrency baseline only with attributable evidence; a competing
configuration change must remain a conflict with recoverable unsaved text. Read-only retry must
not diagnose again. Preserve pending/unknown protections, changed targets, separate editors and
actual owner diagnostics. No new durable replay engine, schema or general concurrency protocol.

Check the former Files P2-R1 operation fence and A1 empty-name/input/provider controls remain
correct; do not rebuild those solutions. Once the S0 bounded regression is fixed, continue A2.

## 3. Complete all six remaining editor sections

Read [SCOPE_AND_ARCHITECTURE.md](SCOPE_AND_ARCHITECTURE.md),
[ACCESS_SCOPES.md](ACCESS_SCOPES.md), [MEMORY_AND_ROOTS.md](MEMORY_AND_ROOTS.md) and
[CAPABILITIES_AND_CONFIRMATIONS.md](CAPABILITIES_AND_CONFIRMATIONS.md).

Complete **Memory, Project Structure Access, Workspace Tools, Secrets, Process Access and
Capabilities**, under the existing form shell. Preserve all ten section identities, route tokens,
loading/failure behavior, existing production entry points and the four completed A1 sections.
The actual substantial markup must move, including memory bindings and external-root entry/
selected-reference presentation. The shared list, picker and neutral controls are reused, not
replaced by new lookalikes. Loading a section never becomes a permission grant or durable write.

Prefer extending `CanDoItAll.AgentFramework.Editor.UI` and its existing independent sandbox.
Split into a second cohesive leaf only if an evaluated dependency/caller reason warrants it.
Do not invent a project or interface per section. One neutral view/intent boundary may describe
several coherent read and effect lanes; it must not hide IServiceProvider, arbitrary reflection,
service bags, callbacks keyed by strings or direct module/backend references.

Keep one canonical AgentEditorSession, complete AgentEditorModel and EditContext per editor
lifetime. Do not build a second writable whole-agent model in the new leaf or split Save into
independent per-tab writes. A presentation controller can be shared where it truly eliminates
production/scenario policy drift; policy extraction is not a license to move persistence.

Complete the small Agent delete, auto-approval and workspace-risk confirmation **rendering** family
as part of this editor: actual text, acknowledgement, confirm/cancel/focus and origin-bound result.
Dialog service/reference and policy approval stay with the production host. Preserve old entry
points when used elsewhere. There is no global close-all or cross-editor confirmation result.

Capability **definition authoring** (CapabilitySetupWizardDialog, definition editor, inline-skill/
MCP/tool authoring and provider executors) remains a real explicit host integration. Its buttons,
creation return, assignment, failure and cancellation journeys are in scope for integration, but
its whole authoring family is not extracted. Likewise retain actual Avatar generation, shared
provider synchronization and the already extracted StorageSelection picker through typed slots
or composition. Do not leave the six complete sections behind those slots: only their explicitly
classified pre-existing reusable or out-of-scope integrations may remain host-composed.

## 4. Keep dependencies directed and data minimal

Read [DEPENDENCY_CONTRACT.md](DEPENDENCY_CONTRACT.md). Baseline project, package, public-type,
runtime and asset graphs, then compare the final result. Protect existing AgentFramework catalog/
Overview/capabilities, Projects P1/P2, Workspace, Resources and generic Conversations/Configuration
roots from new dependencies on Editor.UI. The editor sandbox itself is the deliberate target of
this expansion; its new references must remain backend-free and measured, not hidden.

Use existing lightweight model/abstraction types when actually appropriate. Do not move persisted
agent models or permission protocols merely to rename namespaces. No Foundation/MAF runtime or
Memory.Application/driver should learn about product editor UI. The existing migrations aggregate
legitimately reaches product composition; classify that old aggregation rather than treating a
pre-existing transitive path as a new forbidden direct runtime edge.

The Memory section currently injects a profile store and driver enumeration. Move those reads and
eligibility calculations to their existing host/owner, projecting only the metadata and capabilities
needed for rendering. No HTTP/MCP client, driver activation, credentials, endpoint secrets or
Memory.Application reference in the leaf. Do not enable any previously unsupported Memory operation.

External-root entry currently creates a host registry. The host must still normalize native paths,
export protected root bindings, classify unresolved aliases and check host identity. The renderer
uses safe display data and typed entry/remove intents; it does not manufacture valid aliases,
inspect the filesystem or reinterpret protected tokens. Actual host paths used for an authorized
operator display are not permission to export protected binding payloads into logs or general state.

AgentCapabilityList already resides in a separate light UI family; StorageSelection has its own
leaf. Compose/reuse them honestly. Do not add Core/Voice/Canvas or a whole product module just for
one control, and do not copy the control to avoid an explicit reference. A narrow composition slot
is valid when it hosts the real existing renderer in both production and full-editor sandbox.
Evaluate the real closure before choosing a harmless direct UI reference versus composition.

## 5. Preserve state, authority and operation semantics

Read [STATE_AND_EFFECTS.md](STATE_AND_EFFECTS.md). Capture request inputs and the owning editor,
read lane, selection revision and operation before the first relevant await. Fence successful,
failed and finally paths. Same-origin multiple requests need their own request/attempt identities;
session identity alone is insufficient. Reopening A after B is a new lifetime, not permission for
an old callback to publish. Same-section echoes must not rebuild a draft or rerun queries.

Save/Enter/assignment-triggered Save share actual admission. Existing capability toggling on a
saved agent currently saves the complete draft; preserve and clearly present that behavior instead
of silently making assignments immediate in every context or changing them to an unrelated writer.
New-agent assignments remain staged until Save. Verify, catalog refresh and read-back do not save
the form. A wizard-created capability is a separate committed fact from its agent assignment:
retain its exact ID and origin before any optional catalog read, and never retry creation to repair
an assignment or read-back failure.

Preserve native optimistic versions, managed-agent deletion guards, deletion journal fixes,
project IDs AND acquired lifetime bindings, external aliases AND protected host bindings, storage
allow-lists, specific secret references/purposes, all Memory settings and unknown JSON/extensions.
Test explicit changes from A2 as well as untouched A1 and hidden values. Do not normalize away
restricted/missing stored references merely because a current list failed or no longer includes them.

Project read, non-task write, task write, full write, create-root and create-child permissions remain
separate. Select all observed projects is not AllowAll future projects. Apply in a picker edits the
draft only; it is not Save. Successful loading is not action authority. Actual runtime still checks
capabilities, approvals, project lifetimes, source scope, storage access and external-root restrictions.

Secret editing here is reference metadata only: never call a decrypt/get-secret-value path to render
this section. Do not serialize a whole agent containing protected root tokens into general receipts,
URLs, diagnostics or screenshots. Keep safe identities/stages and exact opaque receipts at their
owner; sanitized evidence must be distinct from private artifacts.

## 6. Build a complete, faithful editor sandbox

Extend the existing editor sandbox to show all ten real sections and the small confirmations.
Use real BaseLib/Conversations components, the real capability list and already extracted Storage
picker with deterministic query adapters. Stubbed markup or backend DI registration is not completion.
Use deterministic in-memory committed scenario state for editor edits/assignments/proof outcomes;
a rendered success message without persisted fixture state cannot demonstrate retry/identity logic.
Label native path resolution, capability-definition creation and external-provider effects as
simulated; their real ownership is proven separately in production. Existing simulator metadata
must not become a second implementation of provider compatibility/security rules.

Scenarios include missing and restricted saved references, no eligible Memory driver, loading and
per-lane error/retry, manual Memory alias edits and ordering, two independent editors, confirmations,
known commit plus failed read, unknown publication, provider refresh, changed profile, late results,
unsaved-before-Verify and during-Verify edits, and a nontrivial large catalog. No paid requests,
credential store, DB, MCP subprocess or uncontrolled native application in the sandbox.

## 7. Validate the product and its real consumers

Follow [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md), [APPLICATION_JOURNEYS.md](APPLICATION_JOURNEYS.md)
and [DESKTOP_AND_DEV_LOOP.md](DESKTOP_AND_DEV_LOOP.md). Build affected production/test projects
before build-backed discovery; confirm current counts and execute the identical filter/configuration.
Current counts are evidence, not the historical numbers in this package. Retain failures and causal
repairs, distinguish repeated/subset cases and never relabel a mixed checkpoint all-green.

Use **1920 × 1080 at 100% zoom**. A second large 1600 × 1000 run is optional only for a concrete
functional question. Do not tune phones/tablets/small or medium screens, introduce their acceptance
matrix or consume the session on responsive polish. Do not delete existing generic tests.

Besides leaf and controlled host tests, require native all-section round-trip, exact capability proof
publication/recovery without hidden Save, project/storage restrictions and host-bound roots,
secret metadata-only reads, assignment/create-after-wizard failures and schema/contract preservation.
Run actual UI-created-agent Project Structure/file/approval and affected Workflow/TestLab/Projects
Files journeys using scripted external responses but real tools, policy, writes, read-backs and
hashes. Check behavior of negative proposals and a second unrelated agent/project. Reuse existing
fixtures rather than seed a replacement shadow application.

No live budget is granted. Preserve the exhausted 40/40 journal; no automatic paid/provider requests,
image generation, voice calls, real email, deployments or broad local scripts. Absence of external
prerequisites is BLOCKED/NOT_RUN, not a pass. Deterministic end-to-end execution must be reported
as scripted-external/native-owners, never as live-model validation.

Use targeted loops during stages. Once code settles, run the complete owning editor/component and
adapter families plus the named consumer journeys. Make and document the broader Stable decision
against actual `docs/testing.md` triggers. Existing registration-only additions are not an automatic
five-hour sweep; an actual shared owner/contract/protocol change may trigger a wider final checkpoint.
Do not repeatedly run unchanged entire suites after each section. Final mandatory portability-static,
reviewed baseline deltas and enforcement without write flag, documentation, asset publish and graph
negative controls remain required even when broad Stable is not repeated.

## 8. Close the editor, not the whole roadmap

Use [EXECUTION_STAGES.md](EXECUTION_STAGES.md), [CLOSURE.md](CLOSURE.md) and [ROADMAP.md](ROADMAP.md).
Fix small demonstrated adjacent regressions with tests. For a problem requiring schema, new durable
coordination or changes across several authoritative owners, preserve diagnostics and map exact
flows/impacts for a separate corrective bundle; stop only the affected unsafe lane, not unrelated
safe work. Do not reopen healthy Workspace/Projects work or expand into the next module for activity.

The final report must distinguish: S0 fixed; ten-section rendering complete; explicitly retained
host integrations; native behavior; test results and blockers; exact dependency delivery; measured
development loop. Report per-stage commits/source trees and known budget/environment limitations.
Update the maintained renderer census with real paths and caller roles, not a count of .UI projects.
A2 completes the ten-section technical editor boundary, NOT provider administration, capability
creation authoring, all AgentFramework chat/usage/team UI, Workflow authoring, Workbench or Processes.
