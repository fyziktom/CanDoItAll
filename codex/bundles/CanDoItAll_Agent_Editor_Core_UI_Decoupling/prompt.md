# Execute: Projects Files follow-up, then Agent Editor Core A1

You are implementing this handoff as a senior C#/.NET/Blazor engineer. Finish the bounded changes,
production wiring, independent sandbox, tests, measurements and maintained documentation. Do not
stop at a plan, after S0, or after merely moving files. Do not begin a second Agent editor slice.

## 1. Read and establish the actual starting point

Read `AGENTS.md`, `.github/copilot-instructions.md`, `docs/testing.md`, `.github/workflows/ci.yml`,
`docs/architecture/ui-component-seams.md`, applicable local instructions and the available
CanDoItAll.SharedInfo standards before editing. Read [shared/README.md](shared/README.md), its
architecture and validation documents, and all files declared in this package's `bundle.json`.
Use the current source and evaluated references, not a default-branch search excerpt.

The reviewed main commit is `92a3c373c742537608fded3c483b685291339853` on `components-decoupling`.
Components development was the published `4a858412d2c2a3f6123bf23d8c4584f05b47627d`;
FileTools inspected source was `3a080ecd31068a77c1e1bd639f7a78e21c93db85`.
These are provenance, NOT execution pins. Record actual HEAD/tree/dirty state in every used
repository and the exact source/package resolution. Preserve other work. The previous P2 bundle
was committed as history; do not execute it again or revise its sealed evidence template.

Use Code Analytics and Components MCPs when available. For this concrete UI work inspect the real
component contracts and examples before choosing widgets. When a tool is unavailable, record that
fact and use evaluated MSBuild, exact source and bounded call-site searches. Do not repeatedly
attempt unavailable tooling or build a new universal scanner. Baseline the protected UI/sandbox
graphs, the technical editor's actual consumers, descendants, assets and existing test discovery.

## 2. S0: close the bounded Projects carry-over first

Read [S0_PREVIEW_OPERATION_FENCE.md](S0_PREVIEW_OPERATION_FENCE.md). Reproduce **P2-R1** before repair:
within the SAME accepted Files activation/workspace, an earlier preview ignores cancellation,
a newer preview or action completes, then the earlier preview throws a non-cancellation exception.
The current `ActivateAsync` catch fences by activation only and can overwrite the newer view's
error state. Existing across-activation A→B→A tests are not this ordering.

Add a deterministic regression using the actual surface session and a real rendered FileBrowser
interaction. Check both later-success and later-failure outcomes, a current failure control and
exact cleanup ownership. Fix publication using the identity of the admitted operation in addition
to activation/context; old error/finally paths may not clear or annotate a successor. Do not replace
this with a global queue, a button-only guard or changes to the authorization/lease protocol.
Preserve stale successful grant cleanup, original request cancellation and native action no-replay.

Correct the maintained module map: Projects Files P2 is no longer deferred. Inspect the actual
UTF-8 title bytes and rendered separator in `ProjectFilesDialogView`; correct a confirmed encoding
artifact only, without a UI polish sweep. Keep historical reports and sealed bundles unchanged.
Re-run the owning P1 refusal and interop controls rather than rewriting their now-correct fixes.
Then proceed directly to A1 unless a newly reproduced data/authority defect makes that unsafe.

## 3. Implement exactly Agent Editor Core A1

Read [SCOPE_AND_ARCHITECTURE.md](SCOPE_AND_ARCHITECTURE.md) and
[AGENT_EDITOR_SOURCE_REVIEW.md](AGENT_EDITOR_SOURCE_REVIEW.md).
Extract the real reusable form/shell and the actual **Identity, Runtime, Images and Voice** markup
from `AgentDetailsDialog`, with loading/failure/warnings, section navigation, validation and the
Save/Clear/Delete presentation. Keep all ten existing section identities and all production entry
points. Existing managed-agent restrictions, confirmations and routes remain functional.

Suggested leaf: `src/UI/CanDoItAll.AgentFramework.Editor.UI`.
Suggested sandbox: `src/Sandboxes/CanDoItAll.AgentFramework.Editor.UiSandbox`.
A narrowly scoped `CanDoItAll.Modules.AgentFramework.Editor.Contracts` is optional only if it has
real boundary value. Reuse equivalent work already on the checkout. Do not create a duplicate
Projects, Workspace, catalog or conversation framework.

Deferred sections are **Memory, Project Structure Access, Workspace Tools, Secrets, Process Access
and Capabilities**. Production must compose their actual existing content through typed,
origin-bound section slots under the same logical editor form/session. Their policy, queries,
capability creation/verification and authority stay with their current owners. They must not be
stubbed, removed or reported as extracted. Preserve their mount/read behavior; switching sections
must not discard drafts, raw validation, pending dialogs or ordinary lazy-read semantics.

The AvatarPicker's provider-backed generation and source-managed provider refresh are explicit
host integrations in this cut. Keep their real controls/actions in production with captured
session/provider origin; a neutral presentation slot is not permission to replace them with a
fake success. The core identity/avatar display, provider/model display and all four selected
sections really move to the leaf. Classify retained host integrations in the completion census.

## 4. Keep the dependency direction honest

Protect existing AgentFramework catalog/capabilities/Overview UI and sandbox, both Projects leaves
and sandboxes, all completed Workspace leaves, Resources, Conversations and Configuration. They
must not acquire the new editor as a dependency. Composition belongs in the product host.
Foundation/Infrastructure/MAF runtime must not reference the new product editor or its contracts.

Inspect actual graphs rather than banning names. `AgentFramework.Models` already has an
abstractions-only declared graph and is used by the existing Agent UI: retaining selected model
or enum contracts can be correct after evaluating the complete closure. This is NOT permission
to pass raw provider credential-bearing objects, private services or the entire provider registry
to the renderer. The broad MAF Components project references Core, Voice and Canvas; do not bring
that graph into a leaf just to reuse a model selector. `ProviderModelSelector` already wraps the
neutral `ConversationProviderModelSelector`: reuse that real neutral component and safe mapping.

Keep thinking-effort compatibility/default/None/unknown policy authoritative and single-sourced.
Use a safe presented option/support contract, or a justified neutral rendering split retaining the
old compatibility wrapper. Do not copy policy into both production and sandbox, silently discard
an unsupported saved override, or add an upward MAF→new-product-UI reference.

A presentation-plus-intents or cohesive view contract is acceptable. Keep one canonical
AgentEditorSession/draft/EditContext. Reusing neutral models is preferable to duplicate mutable
whole-agent state. If a neutral renderer receives a supplied EditContext, it must not cast its
model to implementation services or discover owners through it. Do not use IServiceProvider,
reflection lookup, a service-bag facade or arbitrary callback dictionaries as a hidden backdoor.
Move/serve scoped CSS, real assets and registration needs with their owning renderer. A large
unchanged code-behind is not automatically a fault; duplicated orchestration or a full renderer
left behind a forwarding component is.

## 5. Preserve writes and every deferred field

Read [STATE_AND_COMPATIBILITY.md](STATE_AND_COMPATIBILITY.md). Save continues through the actual
`IAgentEditorCommands` / `IAgentFrameworkWorkspaceService`. A core-only DTO must never rebuild a
whole agent with default permissions, missing configuration extensions or empty deferred lists.
Retain hidden temperature/background/history/template fields, Favorite tags, secret references,
project IDs AND lifetime bindings, process IDs, external-root bindings, storage restrictions,
Memory settings, capability assignments and unknown extension JSON. Secret values are not editor
reference metadata and must never enter a projection, general receipt, URL, log or screenshot.

Preserve original target and expected update version. Capture a complete submission before awaits,
including tags, provider/model/default intent and image settings. One logical save admission must
cover Enter, footer Save and existing capability-triggered saves. A late callback belongs to its
rendered origin, not whatever editor happens to be current. Clear/create is not catalog selection.
Preserve current typed rejected/committed-with-warning/unconfirmed semantics and exact read-only
reconciliation. A refresh must not save, verify, issue credentials or generate images.

Do not weaken optimistic concurrency, managed-agent deletion, approval, workspace sandbox,
project lifetime, host-binding or file authority to make a test pass. Existing Process selection
unavailability is retained; do not add missing Process/Memory/provider capabilities. No DB schema,
new durable replay engine, permission protocol or API-only conversion belongs to A1.

## 6. Verify the actual product, not just a scenario

Follow [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md) and
[APPLICATION_JOURNEYS.md](APPLICATION_JOURNEYS.md). Before each changed selection, build affected
production projects and test assemblies, state/confirm current discovery and execute the exact
matching filter. Keep failing-first and failed attempts; later successes do not relabel them.
Do not assert old fixed counts from this package as discovery on a changed checkout.

The independent sandbox must render the same four core sections and form using real shared
widgets, realistic provider metadata, deterministic save/rejection/warning/failure scenarios and
two independent editor lifetimes. Explicitly label deferred host-only areas; a blank slot is not
proof of their rendering. Publish and run it outside the source tree without product DI/database.
Test actual Unicode typing before blur, supported/unknown effort choices, provider/model ordering,
raw validation, section changes, save/reconcile, close/reopen, stale callbacks and partial refs.

Production proof includes native round-trip of ALL deferred data while editing only core fields;
real confirmation behavior without enabling broad defaults; a chat run by the saved agent with
only the external model response scripted; actual Project Structure file tooling/approval/readback
where the changed editor configuration is consumed; and preservation of a separate other agent.
Use current source-pair evidence for runtime provider/model/effort and file IDs/bytes. Never label
scripted inference as paid live proof. A model's textual success or an HTTP 200 is not an effect
receipt. Refreshing a configuration must not fire the model or a capability verification.

Use task-owned PostgreSQL 18, roots, fixtures, browser contexts and servers. Never use the normal
app on port 5032 or real user data as a fixture; never kill broad process groups. Do not launch
arbitrary applications, external e-mail, payments, deployment or production actions.
**Zero new paid/live-model calls are authorized. The exhausted 40/40 journal remains unchanged.**

## 7. Desktop, time discipline and closure

Use [LARGE_SCREEN_POLICY.md](LARGE_SCREEN_POLICY.md): primarily **1920×1080**, 100% zoom. Optional
1600×1000 only for a concrete large-desktop defect. Do not spend this run tuning small/medium
screens, mobile/tablet geometry, visual redesign or a breakpoint screenshot matrix. Do not remove
existing library tests. Keep focus, keyboard, dialog ownership, full-menu/viewport visibility,
scroll and readable large-desktop content checks.

Measure [DEV_LOOP.md](DEV_LOOP.md) after the code is stable. Preserve P1/P2 watch boundaries. Record
real evaluated graph and observed Razor/C#/CSS edit-to-visible times; do not invent feature JS or
claim whole-Web speedup. Do not benchmark concurrently with expensive builds/tests.

Run broad Stable only for an actual named trigger in current `docs/testing.md`, not because a
phase ended. Explicitly assess moved shared contracts, changed owner semantics, common fixtures,
composition and consumer impact. If a trigger applies, run one frozen final checkpoint and assess
invalidation after subsequent edits. Otherwise document the bounded owning/consumer substitute.
Always close portability-static with reviewed added/stale findings and final no-write enforcement.

Use signed, meaningful local commits under existing policy; never disable signing. No push, PR,
merge, package release or cross-repository bulk cleanup is authorized. Repair small reproduced
bugs in this scope. For a genuinely complex new authority/schema/multi-owner defect, stop the
unsafe affected path, record a precise causal map, effects, repro and recommended repair slices;
continue independent safe work, but do not claim the affected gate passed.

Finish with the actual source pair, S0 disposition, A1 renderer/retained-host census, source and
published sandbox evidence, tests with independent attempt counts, graph/watch results, raw
artifact provenance, safe logs and remaining risks. Update maintained module/local docs to say
**Projects P1/P2 complete; Agent editor A1 complete only for its selected sections**, never all
AgentFramework. [EXECUTION_AND_CLOSURE.md](EXECUTION_AND_CLOSURE.md) defines completion.
