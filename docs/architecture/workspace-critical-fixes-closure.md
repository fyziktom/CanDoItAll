# Workspace critical fixes and closure — 2026-09-30

This is the preceding campaign's retained disposition. See the [R2 continuation](workspace-closure-r2.md)
for the current repair, dependency and regression evidence; the failed results below remain historical facts.

**Workspace rendering remains complete. `ready_for_next_module=false`.** WCL-R1, WC-C1
and WC-C2 are closed locally. WC-C3's original controlled defects are repaired, but composed
navigation remains open under WCL-NAV1. A new Agent deletion failure, WCL-DEL1, also remains
open. Required live/environment proof and remote dependency delivery are incomplete.

This report covers the authorized work executed from the complete extracted package. No new module,
renderer extraction, schema, provider feature, grant expansion or transaction redesign was
introduced. The existing 22-entry census and 11-file Workspace UI boundary are preserved.
The [boundary record](workspace-completion-ui-boundaries.md), [census](workspace-closure-map.json),
[campaign](workspace-regression-campaign.md) and [finding index](workspace-regression-findings.md)
carry the same distinction between structural completion and application readiness.

## Actual source and evidence ownership

| Input | Exact identity |
| --- | --- |
| Entry application | `d9273a88973d28d73c4d29f8686f2b3d68ef8f3a`, branch `components-decoupling` |
| Archive-only package commit | `d8bf5b5da42b13eee225c6501c323c82efe053ab` |
| Initial bounded application repairs | `e6672c14fca3eb2ca22e56771a3bfb2dcedd64e0` |
| Final production application | `3d7c88f384b7920744464a7c7570530ca825325a`, tree `7e289b141c7471b91ce0e17565c5f5c4446ac505` |
| Final Components | `22d5b21afdf80c2bca74c1c598f0b1bb72c86f9e`, tree `1e318a37f187c120e74d88357715ba22ae5cec31` |
| Private observation/catalog commit | `2f658cda606c8ab13f71025ff33023b9c220bcad` |
| Private receipt test correction | `02bbb723396514f035eb4d2748bea71c5899f9e9` |
| Local FileTools, unchanged | `3a080ecd31068a77c1e1bd639f7a78e21c93db85` |
| SharedInfo, unchanged | `83e21e23bcf43d92b061a6d367ac385241d13cd3` |
| Mcp, not edited | `abeaea463aa906bca2d83a1b608ff432dbc29d8d`; eight pre-existing dirty files preserved |

Application and Components commits retain the configured GPG signature and identity.
The production source manifest is SHA-256
`3934881227b0dd2e4243e425be77cc155b521f1f61ced9588d05011c6b5f1c4e`.
The committed observation manifest is SHA-256
`c580732a168d979fbc8b0e79b7ffd463f896e05d640e7b403aa24a5bf56a48e2`.
These are manifest digests, not Git commits. The final maintained-document commit is
documentation only; it does not replace the actual compiled source identity.

Ignored evidence root: `artifacts/workspace-closure/20260930-d9273a889`.
`entry.json`, `frozen-source-02.json`, `observation-source-02.json`,
`observation-source-equivalence.json` and each attempt's source/command/discovery/TRX record
identify the input. The source comparison permits exactly three later files: a canvas
keyboard-readiness observation, exact provider-history identity comparisons, and runtime
catalog metadata. Production, build-graph, Components and FileTools bytes are identical.
The later `receipt-owner-observation-final.json` and `receipt-test-commit.json` separately
bind the one receipt test correction to its actual executed file bytes and signed commit.
Only that test and maintained closure documents differ from the observation checkpoint;
production, dependency and shared fixture bytes remain unchanged.
Focused runs retain their actual uncommitted manifests, which are byte-equivalent to the
subsequently signed observation commit. They are not relabeled as clean-commit builds.

The Stable driver's `head` field is captured at the end of its long run; its original
`source.json`, build evidence and discovery assembly hashes identify what was built.
`stable-source-binding-final.json` verifies that distinction and retained assembly identity.
Earlier interrupted, failed-build, bad-discovery, failing-first and focused attempts remain
separate. Original historical journal/log hashes were checked in place, never reconstructed.
`attempt-history-final.json` indexes every retained raw TRX and its available source record;
an absent source record is marked absent rather than inferred. Repeated runs are not summed
as unique coverage.

CodeAnalytics, Components and dotnetwatch MCPs were unavailable. Current source, exact sibling
contracts, evaluated MSBuild dependency/watch inputs, owning tests, CLI build/publish and
Playwright supplied the recorded fallback. No default-branch search index was treated as
current source.

## Repairs and controlling proof

| Finding | Bounded change and proof | Disposition |
| --- | --- | --- |
| WCL-R1 | Editor-specific Save/Delete validates exact existence, kind, current/pending/startup protection inside catalog coordination against trusted runtime state. Legacy non-UI Save keeps its upsert contract. Two-owner write-window controls fail before and pass after; the complete admission/acknowledgement set passes 10 cases, with two Partial UI controls. | Closed locally |
| WC-C1 | The runner retains admitted/waiting participants and its gate until settlement. Each admitted operation owns its actual async DI scope and resolves scoped dependencies inside it; original profile/token ownership is retained. Contributor retirement fences later publication. Runner, real EF scope and contributor reproductions fail before; 4 + 1 + 1 focused controls and final consumers pass. S0 nonblocking initialization remains. | Closed |
| WC-C2 | The owning BaseLib Dialog/interop caches retirement work, rejects late import/open, releases module and callback independently, and preserves unexpected active faults. JS owns exact dialog identity, detached cleanup, focus and scrolling. Cancelled-disposal reproduction, nine focused controls, 443 sibling tests, actual published consumers and exact assets are retained. | Closed locally; publication pending |
| WC-C3 | MainLayout fences each awaited stage by layout/route/profile identity and releases only its own listener. Browser state rejects stale profiles and disconnected Save acknowledgement; Workbench starts only after acknowledged persistence. Three layout and one store failures precede passing five layout/three store controls and held-query shared UI proof. The full shared log still records WCL-NAV1. | Original controls repaired; composed closure open |

WCL-R1 also distinguishes a known catalog commit followed by selection failure: the typed
`DatabaseProfileCatalogCommittedException` retains the exact committed ID. The owner/session
returns Partial, retains the user's raw draft/EditContext and progress, and never replays
the upsert. Blank-password retention, exact create identity, physical data retention on
profile deletion and canonical A-before/B-after-owned-restart semantics remain covered.

Two additional small defects were reproduced and repaired before the final production
freeze. Floating Agent initialization now retains its captured token and fences held
settings/catalog/maintenance work after retirement (two failures before, all 13 focused
controls after). A disabled native opener's focus loss is handled by the exact BaseLib SDK
initializer, without changing Button or stealing unrelated focus; its actual browser
regression fails before and passes after. The final broad inventory uses both repairs.

The [Simple Chat](workspace-shared-shell-lifetime-finding.md),
[Dialog](workspace-dialog-retirement-finding.md) and
[browser-state](workspace-browser-state-retirement-finding.md) records retain their original
histories and current dispositions. No ObjectDisposedException success path, renderer/JS
teardown drain, global timeout change or weakened full-log assertion was introduced.

## Harness safety and architecture

WC-H1 is closed. `LiveUiHost`, `UiEvidence`, `AgentUiJourneySupport`,
`ProjectFilesUiJourney` and `ScriptedAgentUiFixture` are cohesive top-level owners.
Project Files, Workflow, Agent runtime, file harness and shared-host journeys have separate
classes. The two original CRM/HR case identities remain; ten moved cases are reconciled in
`test-identity-moves.json`. The GeneratedRegex support partial is compiler plumbing.

App stop is cached and idempotent and retains the database/evidence roots until read-back
and final cleanup finish. The ten-tool-batch watchdog is independent of the proxy's 10/40
request controls; the first typed stop is retained. Approval binds to exact original run,
project/parent/context generation, proposal, path, content, media and overwrite policy.
Changed/unknown proposals and external URLs are refused; delayed approval is admitted once
without replay. Deterministic controls exercise actual MAF, Workflow, owner effects and UI.

Safe allowlisted outbound HTTP/provider-terminal metadata is separate from reservations,
tool batches and durable effects; no response/credential dump is used. SSE observation is
bounded. The explicit 150-token forwarding control passes, and HTTP 200 with incomplete
Responses is refused. That does not establish the cause of the historical live failure.
Harness readiness is demonstrated by these deterministic controls, not by a new live pass.

## Fresh validation results

The full Stable checkpoint completes **15,866 executions across 19 projects: 15,865 passed, one failed, zero skipped**. Discovery lists 15,811 cases; recorded theory expansion accounts for the difference (Unit: +34, Integration: +5, Memory.Tests: +16). Elapsed wall time is 179.5 minutes. The receipt test correction below subsequently passes all 31 owning-class cases. The original full gate remains FAILED; this is not a second all-green complete Stable run.

Three Release roots were built before discovery: the production solution, Stable solution
and Playwright solution. The broad trigger is the changed shared owners, DI lifetime and
Components consumption, together with this package's explicit full-checkpoint requirement.
`frozen-stable-03` uses the documented Stable filter, with live flags disabled and a private
PostgreSQL 18.6 instance. Discovery and every theory expansion are reconciled per project.

The one failing case expected a public Simple Chat operation to flush an unrelated edit
tracked by its caller's DbContext. Per-operation scope ownership correctly prevents that.
The private test now invokes receipt rollback on its actual tracked owner with an explicit
operation context, verifies that a separate public operation leaves the unrelated edit
pending, then explicitly saves that owner and confirms no abandoned definition/receipt.
All 31 cases in `LlmChatDefinitionCreateReceiptIntegrationTests` pass in
`final-receipt-owner-observation-01`. No production or shared fixture changed. The original
complete checkpoint is retained as FAILED in GATE-01; the focused correction does not turn
it into a second complete all-green run. Current owner-group evidence selects the separately
identified receipt cases and keeps the raw full-run result alongside them.

`frozen-browsers-02` runs the complete non-quarantined, non-live current inventory on a fresh
owned Windows host: **171 executions, 157 runner passes, 14 runner failures, zero skips**.
All full-server-log/circuit/asset assertions remain active. After separately recorded exact
case follow-ups, the semantic inventory is **162 PASSED, 2 FAILED, 6 BLOCKED, 1 NOT_RUN**.
This aggregate is not a second complete all-green browser run. Three truncated theory display
names collide; all three distinct execution identities are retained, preserving 171 cases.

Safe follow-ups on the same published production pair pass empty-client shared import plus
scripted Agent/chat/image/vision effects, existing-catalog selection, two avatar cases and
actual Ollama inventory refresh/mirroring/prices. Ollama used existing models for tags/show
only: no inference, model download or new real-model request. The history follow-up passes
all exact credential-filtered global/provider IDs but fails actual Agent deletion cleanup.
The failed case is not reclassified as a prerequisite issue.

The note observation now waits for the real canvas key handler before Tab. Its three-case
rerun passes. The preceding supplemental run has an image-selection timeout in one of those
cases; the unchanged rerun does not prove a production repair of that intermittent failure.
All raw attempts are retained.

The additional joined production journey deliberately holds a real PostgreSQL catalog read,
retires its circuit, and repeats Settings/API/Recovery/Data Sources/Agents/Simple Chats/
Collaboration/TestLab/Project Files, overlays and back/forward in independent circuits.
It passes. Isolated six-case investigation and eight usage/dialog/context-close repetitions
on each final Windows/Linux published host also pass; they do not erase the failed broad log.

| Environment / lane | Actual result |
| --- | --- |
| Windows runtime portability, canonical catalog | 436 Unit + 44 Integration + 1 Browser passed; Debug outputs deliberately isolated from the active Release Stable run; before/after source manifest unchanged |
| Linux runtime portability, canonical catalog | 436 Unit + 44 Integration passed on clean owned clones of observation commit `2f658cda`; no Linux Browser claim |
| Linux core portability | 160 Unit + 15 Integration passed on final production source |
| Published Windows headless | Two outside-checkout startup/restart cycles and browser smoke passed |
| Published Linux headless | Two outside-checkout startup/restart cycles passed |
| macOS / remote CI | Unavailable; no pass claimed |

Windows used SDK 10.0.303/runtime 10.0.12; Linux used SDK 10.0.302/runtime 10.0.10.
The runtime catalog's 45-to-44 Integration correction is traced to the already removed
one-shot Tailwind retry test in `4476b39a15de3a63618bbea6e6a1d7e44243e193`; watch restart unit
controls replace it. All seven Integration class guards remain. Both canonical reruns pass.
The earlier catalog rejection and failed Windows path/configuration attempts remain retained.

`closure-results.json` carries all **35 groups**: the original 29 plus six closure groups.
`group-manifests/` lists each supporting owner/UI identity without adding overlapping cases
to a campaign total. `browser-semantic-final.json` contains raw outcomes, every selected
follow-up, actual source binding and current blocker. Bookkeeping/hash validation is
separate from application execution and never certifies authenticity or readiness.

## Open findings and unexecuted obligations

- [WCL-NAV1](workspace-navigation-acknowledgement-finding.md): the original full host logs an
  unhandled RemoteNavigationManager cancellation for `/agents`. Its error is 59.999818 seconds
  after the usage-scope test's context disposal, but there is no proven caller/circuit mapping.
  Application admission versus framework acknowledgement must be isolated before a remedy.
  WC-C3's composed navigation boundary remains open.
- [WCL-DEL1](workspace-agent-deletion-closure-finding.md): actual cleanup cannot confirm the
  exact Agent deletion after completed runs. Its original session/run/index identities,
  unchanged catalog read-back, encrypted original workspace and original client database are
  retained. Transient runtime keys were cleaned; these are state artifacts rather than a
  replayable runtime backup. The hidden owner
  exception and multi-file/projection commit boundary require bounded investigation. No force
  deletion, state patch, grant change or replacement fixture was used to make the test pass.
- LIVE-01, LIVE-02 and LIVE-03 are **BLOCKED_AUTHORIZATION** on this source pair. The prior
  forty reservations are exhausted; the historical LIVE-02 pass is not transferred forward.
  The journal remains at 40/40 with SHA-256
  `f3b88f709b6d26e35a6c3792930608b3937aa77e3d230dfb2fb3eedb854712e4`.
  Newly authorized requests: **0**. New real-model requests issued: **0**.
- Five broad browser rows need actual shared/local model inference and remain authorization
  blocked. One real OpenAI inventory row lacks an authorized external credential. Scenario04
  lacks its generated application URL/output root and returns before opening a browser;
  its runner pass remains semantically NOT_RUN. macOS proof is unavailable.

The two newly discovered issues meet C8's stop-and-map boundary for an unproven shared
lifecycle or multi-owner transaction problem. The unsafe lanes stopped and independent
safe validation continued. The proposed bounded investigations are in their finding records;
mapping is not a fix or a readiness waiver.

## Components and dependency delivery

The local signed pair is **LOCAL_SOURCE_PAIR_VERIFIED** in source mode. Components' owning
solution/assets/Tailwind/package gates pass **443 tests** (BaseLib 189, Common 5, Gantt 85,
QR 9, WebGL 70, Run 85). Its BaseLib 0.3.0 package records the exact repository commit and
SHA-256 `cb812caab10a9196af0cd360d5b8cca4e38125bab801370c1fd6cfb9b812d185`.
Dialog JS and the fingerprinted SDK initializer agree byte-for-byte between current source,
package and actually served published assets. Published Windows/Linux assembly versions and
hashes bind application `3d7c88f3` to Components `22d5b21a`. Package-mode app execution is not
claimed; the actual consumer ran sibling source.

Thirty actual parent/child production Dialog controls and nine owning-sandbox controls pass
at widths 1600/900/390, covering Escape, backdrop, focus return, unrelated ownership and
body scrolling. Twelve normal/open-overlay screenshots were inspected. The Workspace app's
existing desktop composition remains; smaller widths are proof of the reusable BaseLib seam.

All eight protected evaluated graphs retain project/package/edge/native counts and introduce
no unresolved reference or cycle. Watch inputs add the BaseLib initializer; Web additionally
adds its bounded MainLayout lifetime companion and typed committed-catalog exception. Graph
comparison uses verified historical graph evidence and unchanged build inputs; it is not
presented as a remeasured entry graph. `graphs-final-02/comparison.json` records that limit.
The Web source graph contains 148 application, seven Components and eight FileTools projects;
those are exactly the three repositories in the frozen manifest. The unrelated dirty Mcp
files are not source projects in this consumer graph.

Remote consumption is **BLOCKED_PUBLICATION**. Current CI chooses the Components branch using
`github.base_ref || github.ref_name` and resolves one immutable SHA in its dependency job.
Publish/merge Components onto that exact target branch first, then record/revalidate the
resolved SHA before publishing the dependent application. Pushing only the repair branch
does not satisfy a PR targeting `development`. The unchanged FileTools CI pin
`498b36825bd5a5222429972af120b04becf4b3f6` differs from locally tested `3a080ecd`; reconcile it
before calling remote CI equivalent. No push, PR, merge, package upload or deployment occurred.

## Static review, cleanup and closure gates

Portability-static scan/enforcement passes with **15,214 reviewed findings**, no new/stale
unreviewed delta and final enforcement **without `--write-baseline`**. The final observation
changes are included. Final source/evidence review covers 1,153 artifact text files and 1,312 supplemental source/text inputs. All 70 artifact matches and 23 source-expression/fixture matches have explicit non-secret dispositions; no exact task credential or JWT-shaped token is found. Oversized/unreadable text counts are zero. Binary/pixel and encrypted-private limits are explicit in `secret-review-final.json`; this is not a claim of universal secret absence.

All 11 verified task-owned containers, eight exclusive volumes (including the original PostgreSQL anonymous volume), one task network and the exact task-built upstream image tag have been removed. Seventeen verified private/publish roots, plaintext key/env files, the PostgreSQL credential and private scan sentinels were removed after proof capture. Raw attempts, complete logs, source manifests, packages, served JS, screenshots and four exact published assemblies remain ignored. The encrypted original Agent workspace `agent-deletion-private-snapshot.dpapi` and client database `agent-deletion-private-database.dpapi` remain private with hash/restore receipts. Transient runtime keys were cleaned; these are state artifacts, not a replayable runtime backup. Shared SDK/PostgreSQL base images and normal build/package caches remain. Ordinary port 5032, unowned Docker resources, the original exhausted journal and unrelated Mcp changes were preserved. `cleanup-final.json` names the exact released resources and retained artifacts.

The sealed package validator passes (52 files, 51 manifest entries, 38 source records,
44 local links). Package/closure validator controls (11/23), shared tooling controls (14),
portability baseline controls (6), and secret-scanner controls (4) pass; these are tooling
checks, not application coverage. The final documentation/evidence checks and external
closure ledger validation are recorded in `docs-validation-final.log`,
`docs-evidence-final.log`, `closure-validation-final.log` and `closure-static-final.json`.
The closure validator is run **without `--require-ready`**.

The bundle/work-unit semantic gate is **Fail for application readiness**, with separate
authorization/environment/publication blockers. WCL-R1, WC-C1, WC-C2 and harness correction
work have bounded accepted proof; WC-C3 composed closure and the new Agent deletion issue
remain open, and the original full Stable gate remains failed after its focused test repair.
No phase is silently declared ready and no operator waiver is synthesized.
The next module remains outside this task's scope.
