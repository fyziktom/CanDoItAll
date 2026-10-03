# Execute Provider History PP3 after the bounded PP2 follow-up

You are the senior C#/.NET/Blazor implementation owner for the next UI-decoupling slice.
Complete the work, tests and signed local commits. Do not stop after an inventory or the Tooltip fix.
Use discretion about class structure; the requirements are semantic boundaries and real behavior,
not quotas for interfaces, projects, partial files, test cases or commits.

## 1. Read, reconcile and protect the entry

Read current `AGENTS.md`, `.github/copilot-instructions.md`, `docs/testing.md`,
`.github/workflows/ci.yml`, `docs/architecture/ui-component-seams.md`, applicable sibling instructions,
and the available CanDoItAll.SharedInfo engineering standards. Then read:

- [PP2 implementation review](PP2_IMPLEMENTATION_REVIEW.md) and [Tooltip follow-up](S0_TOOLTIP_LIFETIME.md).
- [Scope](PP3_SCOPE_AND_ARCHITECTURE.md), [source map](HISTORY_SOURCE_REVIEW.md),
  [state/authorization/content](STATE_AUTHORIZATION_AND_CONTENT.md).
- [Validation matrix](VALIDATION_MATRIX.md), [journeys](APPLICATION_JOURNEYS.md),
  [multi-instance runbook](MULTI_INSTANCE_HISTORY_RUNBOOK.md).
- [Dependencies](DEPENDENCIES_AND_DELIVERY.md), [desktop/dev loop](DESKTOP_AND_DEV_LOOP.md),
  [commits](COMMITS_AND_SIGNING.md), [closure](EXECUTION_AND_CLOSURE.md).
- [Shared foundation](shared/prompt.md) and its boundary, state, sandbox and validation guidance.

Record actual HEADs, branches, dirty/index state, SDK and source/package resolution before editing.
Reviewed main is `643a295e112ca29835323501907bf1d8920a5945`; reviewed Components is
`4a858412d2c2a3f6123bf23d8c4584f05b47627d`. Do not reset, switch branches, delete historical bundles,
amend pushed commits or absorb unrelated user work. Historical bundles remain until pre-merge cleanup.
Query CodeAnalytics/Components/dotnetwatch MCPs when available. If unavailable, record the limitation
and use inspected source, evaluated MSBuild, exact CLI discovery and real browser evidence.

Arrange native OpenPGP unlock early. Use the existing identity/key, host user and persistent signing
session. Never request a passphrase/private key in chat, copy it into a container or disable signing.
Make coherent signed checkpoints and verify them; see the signing document. No push/merge/release.

## 2. Preserve what is complete

PP2 now has real Sharing/Sources/refresh renderers, independent sandbox and native consumer evidence.
Retain the local baseline/token conflict repair, coherent imported catalog/editor revision,
exact opaque routing IDs, display labels, canonical relay configuration, accepted write receipts,
recovery/delivery and deferred same-provider refresh until active acknowledgement completes.
Do not repeat the entire old S0 campaign merely to start this task. Review source drift and use a
bounded baseline. Old test outputs are credited with their source hashes, never relabelled as new.

## 3. S0: close the bounded Tooltip cancellation path

Use the reported detached-circuit cancellation and the current Components TooltipInterop code.
First reproduce canceled module disposal, delayed import success/cancellation during disposal,
queued anchor/clear/clamp work and repeat disposal. Existing tests cover success, not cancellation.
Implement the smallest lifecycle-correct repair in the owning Components family, not a copied
application tooltip or a global JS exception suppression. Keep real active-view failures observable.
Ensure owned references and the gate are released correctly and no late callback targets a successor.
Add component/browser teardown evidence on ordinary provider/history controls and nested dialogs.

A sibling change is authorized ONLY for this bounded Tooltip family/tests and necessary delivery
metadata. The earlier Dialog repair stays intact. Record the actual signed sibling revision and
prove the app/test host loads it. Do not pretend that a local unpublished fix is delivered to CI.
If the original raw logs are unavailable, say so; deterministic reproduction and the real caller
check must stand independently. Do not call a logged observation a newly introduced PP2 regression.

## 4. Complete the entire Request History UI family (PP3)

Cover BOTH global `/agents?tab=request-history` and the saved-provider History tab. Complete:
filters including More filters, immutable applied query and pagination, loading/cancel/clear/error/
coverage states, metadata details with exact canonical owner references, and a separate bounded
read-only content dialog loaded only on explicit request. Preserve every currently supported field,
state, zero-versus-unavailable distinction and existing lazy behavior.

Prefer extending `src/UI/CanDoItAll.AgentFramework.UI/History` and the existing independent
AgentFramework sandbox because the results/metadata/content renderers and History.Abstractions
reference already exist. A dedicated cohesive History.UI leaf is optional ONLY after measured
closure/consumer analysis; move the entire family with compatible public types, no copies and no
History leaf depending back on the broad parent. Do not add a new library just to match other bundles.

Production retains authentication/profile notification wiring, original authorized history service,
canonical content owners and permission evaluation. Reuse IProviderRequestHistory and existing
neutral types when they really remove the dependency. Never pass a service bag, EF context,
application implementation or a captured grant as UI authority. Do not add HTTP just for the move.

The boundary must move actual markup, descendants, CSS/assets and required state, not only a wrapper.
One view contract or a presentation/intent family is sufficient where appropriate. The host may
retain route/DI/context ownership. No need to remove every Razor file from a product module.

## 5. Preserve state, query identity and confidentiality

Mounting a tab, editing a filter, opening More filters, rerendering or changing providers must not
query history. Search is explicit. Metadata is explicit after choosing a row; content is a SECOND,
separately authorized action. Do not prefetch contents, run health/model calls, warm full history
or create runtime operations just to render.

Capture a validated query and fixed UTC interval before awaiting. Keep draft filters separate from
applied query; Next/Previous use the applied query, not later typing. Opaque cursors stay bound to
authority, partition, filters and ordering. No local reconstruction or broadening after a refusal.
Preserve 24h/7d/custom, half-open UTC intervals, current 31-day limit, 1–200 page size, previous-page
bound, exact identifiers and canonical external-reference semantics. Use current owner contracts,
not hard-coded copied backend limits if they expose constants.

Fence results, errors, completion flags AND UI intents by their actual activation/query/page/detail
origin. A late callback must not clear another search, cancel its successor or reopen a closed detail.
New Search, scope/profile/auth replacement, explicit Cancel and Dispose have distinct semantics.
Retire only owned requests and presentations; do not cancel an agent/workflow because History closed.

Backend authorization stays authoritative on every read. Permission to see a row is not permission
to read its content or all providers. A single-provider view cannot widen itself through raw filters.
Do not fabricate HistoryAccessContext/AuthorizationStamp in a renderer. On retirement remove retained
sensitive content and callbacks; do not rely solely on eventual garbage collection or hidden DOM.
Encode untrusted text, retain redaction/truncation/capture counts and canonical-owner labels.
No bearer, password, private payload, token-bearing URL or raw conversation in shareable artifacts.

## 6. Finish the sandbox and actual consumer proof

The independent scenario host renders the EXACT full workspace and details/content controls.
No production module/runtime/EF/vault/HTTP registration. Fixture reads are bounded and record which
layer was called; they must distinguish no query, metadata only and authorized content, not return
a static success for everything. Cover realistic/large/empty, incomplete coverage, failure, denial,
expired/pending/redacted/unavailable content, delayed reads, two independent workspaces and retirement.
Do not give fixture data a name implying a production test was passed.

Validate source and independent publish on 1920×1080, 100% scale. No small/medium/tablet/mobile tuning.
Keep large-desktop focus, keyboard, scroll, hit-tested menu/footer/dialog geometry and real assets.
Measure repeat edit-to-visible Razor/C#/scoped-CSS updates and exact graph/watch set. Report actual
hot reload versus rebuild/restart distinctly; never turn a smaller graph into an unmeasured speedup.

Run the mandatory native/multi-instance History journey on owned central/client-a/client-b fixtures:
actual requests → exact caller/provider/model/attempt identities → global/single-provider agreement →
pagination and explicit authorized content → denied public/limited requests → credential rotation.
Use the existing PP2 harness; do not make ordinary retained data a fixture. Include Agent Project
Structure file create/attach/read-back, Simple Chat transcript and Workflow/TestLab evidence from
real owners with only the external model scripted. Default/non-default shared names and routing
remain correct; no paid calls and no reset of the exhausted 40/40 live journal.

## 7. Tests, fixes and final delivery

Build changed production projects first. Discover each exact test filter and state expected/actual
counts before running it. Use current binaries for no-build. Retain failed attempts and scoped
follow-ups separately. Cover current History tests and relevant shared component consumers.
Do not run all Stable after each stage. Decide the final broad checkpoint using current repository
invalidation rules; a shared BaseLib lifecycle change warrants an explicit impact review and a
final frozen checkpoint if required. Preserve the qualified PP2 16,144/16,159 checkpoint as history.
Provision sufficient owned PostgreSQL capacity; do not repeat the recorded 1 GiB tmpfs failure.

Run mandatory portability-static, review genuine findings, inspect any intentional baseline change,
then enforce without a baseline-write flag. Run maintained documentation and safe-export checks.
Do not weaken scanners or ignore every log error. Distinguish expected tested refusal, product fault,
fixture limitation and historical warning; retain exact source/image/circuit provenance.

Repair small reproduced slice defects with regression tests. If a genuinely larger authorization,
schema or multi-owner protocol change is necessary, isolate/map it precisely and stop only the unsafe
path; never silently redesign the platform. No provider diagnostic/model-maintenance authoring,
capability/team authoring, Workflow canvas, Workbench or Processes extraction in this run.

Complete signed checkpoints for the bounded prerequisite, the cohesive History UI/host work and final
test/documentation closure. Return: changes, remaining limits, source/dependency/image pair, test
attempt matrix, measured dev loop, commit verification, safe artifact locations and owned cleanup.
Separate PP3 completion from whole-application release readiness and dependency publication.
