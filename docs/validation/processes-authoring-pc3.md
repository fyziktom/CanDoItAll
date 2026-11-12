# Processes authoring PC3 validation

Status: complete through the frozen campaign and reviewed affected repair closure.
The original full Stable campaign remains `FAIL`; its 20 failures have exact passing
replacements. Counts, source bridges and retained failed attempts are recorded in the
[machine-readable evidence](processes-authoring-pc3.json).
The [architecture record](../architecture/processes-authoring-pc3.md) describes the
implemented contract and intentional remaining owners.

## Candidate and provenance

The task started on `components-decoupling` at
`b24ca3413e1078770eb08877a4936b41df687e71`, tree
`9c9b2cb31c6edcb7d6161c01516f94be4f5a4d57`, with a clean index and worktree.
The reviewed source in PC3 remained unchanged at entry; intervening changes were bundle
inputs. Work continued on the supplied checkout without resetting it.

Consumed local sources were Components
`f3745356182444656edfdef56aa827095632d3ea` and FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`, both clean. The latter differs from the
CI pin; this report describes the actual local pair, not execution against that CI pin.
Validation uses Windows, SDK 10.0.303 and PostgreSQL 18.6. Development and isolated
diagnostics used `ProcAuthoringPC3`; the complete frozen campaign and canonical repair
validation used Release. No CI, other-platform, deployed
database or paid/live-model result is claimed.

The existing `UI_Decoupling_Shared_Bundle` was compatible. Its installed package identity
was `CDA-UI-DECOUPLING-SHARED-v4.1`; this is provenance, not a replacement/version pin.
The final shared decision is `NO_CHANGE`. PC3's sealed inputs are also unchanged.

## Signed implementation checkpoints

The operator confirmed the configured key was unlocked locally. One retained committer
environment used OpenPGP fingerprint
`96E836FAA8854EE98ABC10903C206549E1D7EAD6`; no private signing material entered evidence.
Each listed checkpoint was checked with `git verify-commit --raw` and its expected parent.

| Checkpoint | Commit | Tested responsibility |
| --- | --- | --- |
| S1 | `210da4b7c1e3ee73a98225a00704563772edf568` | Atomic aggregate, receipts, publications and additive migration |
| S2 | `dfaf21ee6c814c86223e23dc98d2e41647a4241c` | All five native authoring families and catalog |
| S3 | `707530eb52de0cf772f12a27769a5195245acb18` | Observation, dirty drafts and operation recovery |
| S4 | `508477290e7131482d8d2acb912e66da4ec9e734` | Immutable publication and exact launch/child resolution |
| S5 | `cf40cb6e094d5aa5989355e726de07970d1b306b` | Scope, transfer, migration and query closure |
| S6 | `13f0e4f2f8612c8c09af7d8dff9245bf9a4ef28f` | Native browser persistence, observation ordering and actual OS restart |
| S6 follow-up | `d805cdde7ba6f10164dd245070997d14ec797c0b` | Correct overlapping definition totals and await Workbench viewport readiness |
| S7 repair | `c27ed9bd12427f57c4384e741877a7e5ecf980f8` | Preserve direct native project launches, document API fields, classify the host fixture and guard pending launch openings |

The complete Stable campaign froze executable source at `d805cdde7ba6f10164dd245070997d14ec797c0b`.
The validated repair source is `c27ed9bd12427f57c4384e741877a7e5ecf980f8`. All 8 source signatures and their
complete parent chain were reverified. The final report-only child is verified after
creation and identified in the delivery, avoiding a circular self-hash in this report.
No push, merge or deployment is part of this execution.

## Selection and result interpretation

Fresh discovery of available MCP tools returned no CodeAnalytics, Components or
dotnetwatch tool. No index was queried and no index freshness is claimed. The fallback
examined source symbols/callers, all affected partial tests, native DI, HTTP/agent/Workbench
producers, serializer/template consumers, deferred Razor children, JS/static assets,
evaluated project assets and the PC1/PC2 floor. New files were included explicitly.

Exact commands, expected and observed discovery, runtime counters, durations and private
artifact hashes are in the JSON. Discovery and execution use the same filter and refreshed
assembly. Dynamic theory expansion is reported separately. Overlapping lanes are not added
together as a unique-test total. Earlier failures remain separate attempts; later affected
reruns replace outcomes by test identity, not by discarding failed cases.

The expanded native attempt executed 481 cases: 477 passed and four failed on stale model
cardinalities. The follow-up retains those cases and adds the other affected migration
fixtures. Browser failures uncovered a pending lazy-read reconciliation defect, an automatic
definition-selection change after a renamed catalog row reordered, and missing readiness in
test navigation. The selection fix pins the resolved key for subsequent reads; a real
pending native Save and two independent editors reproduce its pre-fix failure. Canvas tests
were corrected to hide an obstructing floating panel and invoke Clone through the actual
node context menu. These attempts remain in the evidence history.

Final screenshot inspection also exposed an overlapping-count defect: a launchable
definition with an authored draft was counted twice by the header. The complete All scope
total now supplies workspace/live headers, tab counts and agent context. Regression proof
checks both UI hosts, real native draft/catalog reads, paging and the actual browser header.
The first Release preparation was stopped before Stable execution; it is retained as an
interrupted preparation, not a completed Stable campaign.

The count-corrected full browser run passed 20 of 21 cases. Workbench's pointer target was
captured while Fit was still animating: node bounds stayed unchanged for the test's 300 ms
window, while the viewport animation lasted 320 ms. Readiness now observes actual pan,
zoom and host geometry. Its refreshed rerun passed the unchanged reviewed launch, exact
native workflow and durable project-link assertions. That earlier 21-identity proof is retained;
the later failed attempts below are closed by the final affected browser run.

The complete frozen Stable campaign executed 17,328 cases after 17,273 were discovered
across all 34 assemblies: 17,308 passed, 20 failed and none were skipped. It took
16,571.564 seconds (276.19 minutes). This whole local aggregate is not the current split
CI job; no CI budget or platform claim is changed. The original campaign remains `FAIL`.

Seventeen failures exercise direct native project launches, Workbench assets, HTTP links
and child recovery. These supported requests may omit a separate `ProjectAdmission`.
The initial comparison treated that absence as global scope even though the executable
closure retained the exact project lifetime. The repair compares a separate admission
when present and always retains the real captured-scope and publication gates. Exact
lookup uses the saved closure and refuses a different project or recreated lifetime.
Six new PostgreSQL regressions failed before the fix and all six passed afterward,
including actual commit-time deletion/recreation and archive refusals. The broader affected
native selection, including complete HTTP and project-structure classes, passed all
494 executed cases after 494 were discovered.

One failure reported 19 OpenAPI description gaps. Existing `Description` metadata now
documents the new observations/publication fields and enables the enum's numeric value
listing. JSON shapes and enum values are unchanged. All 23 API coverage cases passed.
One classification failure identified the editor test's real filesystem fixture; its
class now carries `HostPlatform`. The affected unit selection passed 1,816 cases.
The initial host repair passed 288 cases; final host validation passed 292, including
the four new input/opening regressions. Both source-reading classifier cases passed again
on the final source.

The remaining failure was a connection reset while sending the oversized multipart body
in `Voice_request_body_limits_apply_before_provider_dispatch(transcribe: True)`. All
35 cases of its unchanged voice class subsequently passed independently, including
HTTP 413 and zero provider dispatches. This passing rerun does not establish the reset's
root cause or erase it from the original campaign.

The repair changes three native commit/lookup files, description metadata in six
projection files, one Workbench opening method and six test files. It changes no schema,
model/project/package/DI registration, Razor/JS/CSS asset, authoring semantic patch,
template or transfer format. PC3's validation policy permits bounded repairs with an
explicit invalidation review and affected reruns. Earlier J1–J6, actual OS restart,
migration, semantic and light-render proof remains applicable under that review.

The later three-case browser attempt exposed another Workbench readiness race: Continue
was enabled before authority, target and frozen variables were captured. Opening now
stays busy until that context is ready. A deterministic held-read regression failed
before the correction; the complete 22-case opening/manager-input selection passed with
the fix. The final host run includes the new tests and existing restore, retry, retirement
and navigation cases. Closing and replacing an opening remains allowed.

Both Processes browser cases also lost a draft when the test redundantly selected an
already selected manager and typed into the previous composer while its reload was
pending. Trace and source review established that sequence. The test now waits for the
loaded selector, changes it only when needed, and checks prompt text after blur. Readiness
uses the original action's 30-second budget; an earlier private attempt accidentally used
the assertion default of five seconds and is retained as failed diagnostic history.
Manager chat production code is unchanged. All three final Release browser cases pass
with the original native workflow, streaming, attachment, file, cancellation and durable
link/reload assertions.

Private compilation overrides isolated these UI diagnostics while the broad Release
native run retained unchanged source and binaries. Their effective source manifests and
failed attempts remain explicit in the evidence. The exact same four reviewed source
files were then applied to the checkout and rebuilt normally for final Release proof.
An enforced comparison of changed paths and working-byte hashes retains the earlier
native/API/voice/unit results across this narrow UI/test change; the host, source-reading
classifier and affected browsers were rerun on the final candidate.

| Lane | Final result |
| --- | --- |
| Native authoring, transfer and migration union | 215 distinct cases passed; includes six new native compatibility cases |
| Broader native Processes / authority / lifecycle / transfer union | 707 distinct cases passed; failed attempts retained and replaced by matching passing identities |
| Affected Release native selection | 494 discovered; 494 executed and passed |
| Processes unit, Workbench floor and classifier | 1,796 discovered; 1,816 executed and passed; includes all 60 floor cases with six overlaps |
| Final native Processes and adjacent host/component union | 292 discovered; 292 executed and passed |
| Final source-reading host classifier | 2 passed |
| OpenAPI description coverage | 23 passed |
| Unchanged voice boundary class | 35 passed; original transport-reset cause remains unestablished |
| Light Processes renderers | 21 passed; unchanged source retained |
| Browser identity union | 21 distinct cases passed; final affected Release rerun 3/3, with failures retained |
| Original frozen Release Stable | 17,273 discovered; 17,328 executed; 17,308 passed, 20 failed, zero skipped |
| Reviewed composite closure | 17,741 distinct identities passed; all 20 original failures have exact passing replacements |

The composite contains 15,081 retained unaffected frozen cases,
2,247 frozen cases replaced by passing affected reruns, and
413 additional affected cases. Those additions include
new regressions and the existing focused floor outside Stable trait exclusions; they are
not retroactive additions to the original campaign. Cases use owning assembly, TRX test ID
and full display name together. This preserves dynamic rows sharing an ID and distinct
IDs with colliding display labels. These overlapping
lanes must not be summed. No second full Stable execution on the repaired source is claimed.

Reproduction follows [Testing](../testing.md#processes-durable-authoring-pc3), the exact
filters in the JSON and the repository's [Stable command](../testing.md#broad-stable-gate).
Set `CANDOITALL_TEST_CONFIGURATION` to the owning assembly configuration for browser child
hosts. Use an isolated PostgreSQL fixture with its connection supplied privately. The
ordinary application and user databases are not test fixtures.

## Native effect and browser oracles

The initial positive independent-scope test failed before native adapter repair: an accepted
saved name disappeared in the next scope. Its repaired committed regression requires the
exact name and catalog state from the normal native client. Production proof does not keep
role/step services alive to imitate persistence.

PostgreSQL tests verify concurrent global/project first writes, cross-family revision
conflicts, duplicate operations, changed-payload refusal and inherited-global CAS. Faults
at the real transaction boundary verify rollback before COMMIT and receipt recovery after
COMMIT. Independent scopes verify one resulting head/publication/receipt. Project retirement,
same-ID recreation and profile changes cannot reuse captured authority.

Complete semantic fixtures cover hidden governance, branches, subprocess outputs,
forwarding/receipt policy, workflow bindings, guidance and resources. A 17-type property
manifest detects unreviewed semantic-model growth. All 27 distributed definitions retain
their legacy no-op identities. A non-first step preserves its distinct decision role;
partial child mappings and dangling role references are refused.

The real browser journeys use native production lifetimes and isolated PostgreSQL. The
response decorator only delays or loses native results and injects a read-back failure; it
does not supply projections or durable state. Native queries verify exact revisions and
fields independently of the displayed receipts. Raw invalid numeric input survives lazy
tabs, known acceptance survives a failed read, and explicit unknown-outcome recovery reuses
the original operation identity without creating a second effect.

The owned restart fixture runs Web as an OS process, terminates only its owned PID and starts
a different PID against the same profile/database. It publishes distinctive v1/v2 content,
reopens the editor in another browser context, accepts the original v1 preparation after
v2/restart, and verifies exact admission, plan hash, artifact assignment and typed workflow
completion. A new intent chooses v2. The JSON records the actual PIDs and identities.
Canvas proof uses real pointer dragging and the node context menu; native reads distinguish
visual role references from executable role duplication. Role/artifact/process imports
verify canonical relationships and full content after restart.

The J2 renderer adjustment permits nonmutating browsing while writes remain gated. The role
dialog exposes the existing AddRole command and hides the duplicate background Add control.
The enhanced browser cases add with the dialog open, select another role before the delayed
result, then verify eligible selection and all deletions. During import they change target,
preview and category, then independently verify that the original target was mutated and
later browsing was retained. All four authoring cases passed after the source artifact fixture
and editor-readiness corrections, including the subsequent complete browser run after the
count correction.

Retained browser coverage includes source/published Fast and Parity sandboxes, actual
canvas/graph/chart/Markdown assets, native files/operator/cancel/workflow/chat/attachments,
Workbench project-link delivery and permitted/denied/delayed voice. Screenshots and traces
are private; the final JSON identifies inspected images and their findings. An image is not
counted as an inspection merely because it was produced.

## Measurements and dependency closure

The native query fixture contains 1,001 authoring heads: one selected valid document and
1,000 metadata rows with deliberately invalid content JSON. The catalog succeeds without
deserializing those documents. Three samples per operation were recorded in
`s7-count-native`, while the unit campaign and remaining test builds were running; the query-count
assertions remain committed regressions.

| Operation | SQL commands | Observed milliseconds, three samples |
| --- | --- | --- |
| Catalog, output page of 50 | 1 | 18.5330 / 12.2084 / 16.5250 |
| Selected definition | 1 | 3.9086 / 4.8692 / 3.9158 |
| Durable commit and independent read-back | 6 | 32.6341 / 23.1517 / 18.9858 |
| Retained template constructor path | 0 | 0.0945 / 0.0685 / 0.0557 |
| Publication and prepared preview | 15 | 3324.4325 / 952.8421 / 1098.9394 |

These are workstation observations, not performance ceilings or a controlled before/after
speedup. A former dictionary write did not provide durability and is not an equivalent cost
baseline. Catalog filtering/paging currently follows scoped metadata loading: the query
excludes content/history and avoids N+1 reads, but the page size does not bound the metadata
materialized in memory. No per-pointer-frame write path was introduced.

The evaluated source closure has Web 181 projects / 834 edges, Processes.UI 25 / 42 and its
sandbox 27 / 47, with no cycles, unresolved edges or forbidden light dependencies. Eight
active renderer families and their deferred/native consumers are included in the census.
The final browser campaign measured these development startup paths using
`ProcAuthoringPC3`. Each row is one sample; these are not dotnetwatch measurements.

| Mode | Publish milliseconds | HTTP readiness milliseconds |
| --- | --- | --- |
| Source Fast | Not applicable | 4165 |
| Source Parity | Not applicable | 2504 |
| Published Fast | 11382 | 3575 |
| Published Parity | 8573 | 2415 |

## Schema, static gates and residuals

The canonical pending-model check passes. The reviewed idempotent migration adds exactly
the head, immutable publication and receipt tables with their constraints/indexes/history;
it does not rewrite prior runtime or workflow payloads. Populated predecessor upgrades and
idempotent reapplication compare stored payloads and typed identities. Current model counts
are 164 complete entities and 22 Processes owner entities. Old count assertions are updated
without weakening schema, connection or transaction comparisons.

Portability review adds 14 intentional allowances: four existing generated model property
names, logical-key/case policies, existing process semantic metadata, and canonical template
root/path composition. No machine-specific path or shell assumption is added. The final
scan/enforcement, helper self-tests, package integrity, documentation checks and artifact
secret coverage are listed independently in the JSON. Final enforcement does not use
`--write-baseline`.

Retained authoring facts deliberately refuse unsupported generic project export/transfer
and target restore, including global facts. No authoring archive format or history purge is
introduced. The [migration runbook](../../src/Foundation/CanDoItAll.Migrations.PostgreSql/README.md)
requires stopping old writers and taking a backup; dropping the new tables after writes is
not lossless rollback. Local raw traces/logs/SQL are not committed; hashes identify local
evidence and do not imply remotely downloadable artifacts.

## C# Architecture Gate Result

Status: PASS.

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Informational | Scoped metadata is materialized before paging | Query tests and store catalog query | Record the scaling limit; no constant-memory claim |
| Informational | Generic transfer cannot carry retained authoring history | Owner residue checks and native transfer tests | Keep explicit refusal until a separately designed transfer format exists |

### Dependency direction

Application owns semantic coordination and a narrow storage port; Persistence implements
it; the module supplies admission and composition. Existing references suffice. Light UI
uses observation/value contracts and has no native owner dependency.

### Partial-class policy

No new runtime partial boundary hides authoring ownership. Existing test partials remain
test organization; serializer contexts use required source-generated partial declarations.

### Testability proof

Pure semantic/model/reconciliation tests are independent of Web. Native tests use real
PostgreSQL and normal scopes for transactional guarantees. Real browser tests prove actual
controls/assets and an owned process restart. Critical refusal, race and recovery paths
have exact state/identity oracles, not only counts or non-null assertions.

### Closure decision

Closure is complete through the original frozen campaign, exact passing affected
replacements and the reviewed final-source bridge. The raw campaign remains `FAIL`.
Dependency, native/UI/restart, schema/transfer, portability, documentation, signature and
secret-review gates are recorded independently; the bounded residuals above remain explicit.
