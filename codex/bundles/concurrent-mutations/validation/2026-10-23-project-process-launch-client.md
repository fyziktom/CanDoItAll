# Project Process launch client qualification

Status: qualified, 2026-10-23. [P26 design](../plan/P26-project-process-launch-client.md).
SPA remains on **codex/concurrent-mutations**, with no merge or push to main. This closes
the bounded project launch/recovery surface, not the whole concurrent-mutations bundle.

## Delivered behavior and boundaries

The four existing definition reads accept optional project scope and reuse the current
authoring lifetime/overlay owner. Omission retains global behavior; empty IDs are refused
and missing/deleted projects return 404 through a specific Application exception. Other
owner failures are not relabeled as missing projects. Process read permission remains
unchanged and does not grant execution.

The new read-only intent lookup uses the indexed preparation store and existing caller,
profile and surface verification. It returns the existing observation shape, performs no
write, and can observe an accepted original after host restart and project retirement.
The existing status mapping is shared rather than duplicated. Recovery then supplies the
found admission to the original check, so missing state in another dataset cannot create
a replacement preparation. A missing observation does not prove an in-flight write ended.

The SPA page replaces its project Processes placeholder through the existing route
generator's hand-built registration. The renderer delegates selection, review, recovery,
acceptance and lifetime effects to a typed session. A separate bounded storage codec
owns one tab-local original intent, with no saved review content, credentials or prompt
text. Storage failure blocks dispatch. Original request/execute choices are preserved;
unknown acceptance cannot be discarded just because a later read still says Prepared.
Requests use the generated SDK with a fresh single-attempt transport and explicit limits.
There is no polling or automatic write retry. Project changes dispose the old session;
late responses do not update another workspace. Failed/reconciliation states remain
distinct from completion. Visual review corrected the unsaved blocked-plan heading to
`Readiness review`; successful preparations retain `Saved review`.

No project reference, runtime service/lifetime, migration, business partial, new policy
resolver or cross-module query was introduced. Definition reads retain existing catalog
cost. Intent observation adds only indexed reads and existing selected-run observation,
with no history scan, transaction held over UI/provider work, timer or worker. Whole
catalog capacity and broader runtime fairness are not newly qualified by this slice.

## Executed proof

All **nine backend cases** passed after build-backed discovery matched 5 + 1 + 1 + 2:

| Selection | Cases | Evidence |
| --- | ---: | --- |
| ProcessLaunchWorkspaceApiTests | 5 | Project overlays across catalog/editor/roles/steps, deletion/empty scope, original caller and read-vs-execute permissions, missing intent in another dataset with no writes, actual OpenAPI. |
| ProcessLaunchProducerApiTests selected restart method | 1 | Original accepted admission and intent reads after restart/project retirement, without changing preparation or runtime events. |
| ApiSectionPermissionsTests selected existing-read method | 1 | Global read compatibility and the existing authorized owner flow. |
| ProcessPlanningLaunchApiTests selected public producer method | 2 | Both snapshot modes through preparation, intent lookup, changed defaults, original acceptance, real queued MAF proposal, publication and terminal accounting. |

Application, Modules.Processes, Web and Integration Release builds passed serially in
their standard outputs. The initial Integration compile failed on a missing test namespace
import; it was repaired before discovery/execution. No backend test failed. All **1,239
source pins and 14 output identities** remained unchanged through the observed commands.
The later baseline refresh changes only its one reviewed allowance, count and timestamp;
`post-backend-baseline-transfer.json` verifies all other source and every output unchanged
and retains the nine cases against `candidate-identity-final.json`. The original identity
and verification remain available. The SPA-only helper list was updated after backend
resource reconciliation; that stage boundary is recorded separately rather than rewriting
the original companion inventory.

The actual OpenAPI export has **319 routes**, adds only the intent lookup, and changes
only the four definition-read operations. No existing schema changes or removals occur.
The 4,535,680-byte input has SHA-256
`521713ce16cfe8447d8771f0d43381d592620c4ae225b1bee2388b3ec5812931`.
The installed generator consumed exactly those reviewed bytes. The app overrides the
generated isolated-host default endpoint with its explicit same-origin endpoint.

SPA has **135 passing tests**: 25 generator, 22 actual SDK/transport and 88 app Node cases.
The added selections are seven SDK cases and 25 session cases. They cover optional scope,
opaque keys, bodyless/forbidden/missing responses, durable pre-dispatch retention, storage
failure, duplicate actions, immutable retries, changed catalogs, route disposal, another
project's pending slot, unsupported policy/storage and readiness refusal. Two initial
typecheck attempts exposed the SDK's explicit `void` response possibility; the final
typed object guard rejects it without a fallback. Full app typecheck/build and owned
formatting/lint pass. The existing large-chunk warning remains.

The final compiled page passes **14 browser states at 1920x1080**. Six settled selector
overlays fit the viewport; the page has no horizontal overflow or unhandled browser
error. Four check requests, two identical acceptance/continuation requests and twelve
reads match deliberate actions. Lost responses and reloads issue no hidden write. The
first browser script reached twelve states, then incorrectly expected a fresh form to
retain the earlier selection; the test was corrected to explicitly select it. That failed
attempt remains preserved. After the visual wording correction, all fourteen states were
repeated on the final build and screenshots were inspected. These browser scenarios use
controlled HTTP responses; real SQL/HTTP and queued publication are separate proof, not a
joined browser-to-live-backend claim.

## Architecture, static enforcement and resources

Snapshot `snap-20261023131243-4560d5cf` covers 533 documents in Application,
Modules.Processes and Web, with two scoped project-reference edges and no scoped cycle.
Its scope differs from P25 and does not erase the known Workbench cycles. Existing
ProcessesApi size/member findings remain. Collector diagnostics include 189 WS0002
errors, two DEP0002 warnings, two DI0001 informational messages and three DI0002 warnings;
`hasBlockingErrors` is true. This is not a global health pass. Direct project-reference
review and actual builds confirm the unchanged dependency direction for this change.

Full portability scanned **10,087 files / 47,411 findings**, without truncation, including
the new source. Exactly one reviewed allowance was added: the literal intent HTTP route
matches `/proc`; it performs no OS process discovery. Its fingerprint is
`7b64a6fe5d7a0d9d81c6337e964f72d958ffb65c479d68987c7ecb364d28069f`.
Final no-write enforcement passes with **15,691 allowances**. Scanner tests (6/4/7),
rendering register, maintained-doc validation and whitespace review pass.

The artifact secret scan covers 181 text files with no oversized/unreadable omission and
zero actual private sentinel matches. Its three lexical findings are the reused script's
literal file-read/variable expressions, not stored secret values. Source helpers are
reviewed separately; browser evidence contains no credential values. Both newly owned
bulk scan files were removed separately after their hashes, summary and delta were retained.

The 203.3-second resource observer covered all four test commands and the isolated schema
export. Reconciliation found no new live process, database-name delta or temporary-family
residue. Owned browser contexts close in finally; both preview hosts stop and their port
is released. The shared isolated PostgreSQL fixture is retained for the next required
lanes; ordinary port 5032 and its databases were preserved. Compact scan metadata,
reviewed delta, source/output identities, command/results and browser evidence remain
under `.artifacts/concurrent-mutations/validation/c4-project-process-launch-client` and
`output/playwright/concurrent-mutations-spa-p26-20261023`. New bulk scans are removed after
their proof is compacted. Historical cleanup denials remain explicitly unresolved.

## Remaining scope

Native launch selection requires the unavailable Components MCP; native projection and
Runtime boundaries were not changed to bypass it. Definition defaults/versioning,
planning-publication UI, cross-device pending-intent discovery, parent Fail/Replan and
shared refresh/quiescence, multi-activity lifecycle, governed Workflow activation,
durable manager/chat identity, capacity/fairness, remaining Project and other module
producers, external unknown outcomes and final broad engine E2E stay open. Recovery worker
activation remains disabled. No passing focused checkpoint replaces the historical broad
Stable failures or removes those requirements from the ledger.
