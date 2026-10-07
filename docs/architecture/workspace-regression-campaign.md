# Workspace completion: campaign decision and evidence

## Closure update — 2026-09-30

Workspace rendering remains complete. Application readiness remains **false**.
The [current closure report](workspace-critical-fixes-closure.md) records the fresh campaign on application `3d7c88f384b7920744464a7c7570530ca825325a`
and Components `22d5b21afdf80c2bca74c1c598f0b1bb72c86f9e`, followed by separately identified
test-observation/catalog corrections. The original 29 obligations are carried into all
35 closure groups. The earlier counts below remain historical and are not added to new coverage.

The fresh complete Stable run executes 15,866 cases: 15,865 pass, one fails and none skip.
The private receipt test's owner-context correction then passes all 31 class cases; its
signed commit is `02bbb723396514f035eb4d2748bea71c5899f9e9`. The original full Stable gate
remains FAILED. The complete browser run is 157 passed / 14 failed; separate follow-ups
yield 162 PASSED, two FAILED, six BLOCKED and one NOT_RUN of the same 171-case inventory.
Neither aggregate is presented as a second all-green complete run.

WCL-R1, WC-C1 and WC-C2 have verified bounded corrections. WC-C3's original controlled
retirement failures are repaired, but shared navigation closure remains open because the
fresh complete browser run records [WCL-NAV1](workspace-navigation-acknowledgement-finding.md).
The history follow-up passes its request-identity assertions but its real Agent cleanup
fails under [WCL-DEL1](workspace-agent-deletion-closure-finding.md); that attempt stays failed.
Three live rows remain BLOCKED_AUTHORIZATION, and external/platform/dependency delivery
limits remain explicit. The original 40/40 reservation journal is unchanged.

## Historical campaign record

Entry: `1080c24163afd3cf65fcc68756913c5dd3262a62`, branch `components-decoupling`.
The checkout was clean and its entry change contained the bundle. The reviewed product
commit was `fbfba65de9d3118729b73ce9dcf97f28a219c84c`; the checkout was not reset to it.

**Workspace UI completion: PASS. Application regression readiness: FAIL.** Recovery,
Data Sources and Configuration have separate production boundaries, independent sandboxes
and affected owner/component/browser proof. The complete application cannot be certified:
shared shell/dialog lifetime failures, incomplete live file/Workflow journeys and absent
external browser prerequisites remain explicit.

The [Workspace census](workspace-closure-map.json) maps all 11 remaining module UI files
and 22 total entries, including external consumers and the retired Files wrapper.
The [boundary record](workspace-completion-ui-boundaries.md) records composition,
stage commands, dependency graphs, inspected UI states and measured development loops.
The [findings index](workspace-regression-findings.md) separates bounded repairs from
open findings and harness corrections.

Raw evidence is private, ignored and retained at
`artifacts/workspace-completion/20260930-1080c2416`. Its `campaign-results.json` is the
append-only working copy of the sealed package template, with source and artifact hashes.
No raw TRX, log, trace, transient database or credential is committed.

## Final campaign groups

Each row has its own proof layer. A passing Workspace or deterministic row does not
override a failed live or application-wide gate.

| Group | Result | Determining proof or limitation |
| --- | --- | --- |
| BASE-01 | PASS | Acquired catalog draft, Storage Selection lifetime, real Agent Apply/Cancel/Save and backend policy. |
| WS-01 | PASS | Two Recovery feeds, exact detail, explicit action, stale/denied/closed-read controls. |
| WS-02 | PASS | Real prepared Workflow outputs, original receipts/file hashes and owner continuation without a new model/run/upload. |
| WS-03 | PASS | Private profile save/create/schema/test and real input preservation, including failing-first WC-F6. |
| WS-04 | PASS | A-to-B transfer; confirmed preference group survives a later Project refusal; no replay. |
| WS-05 | PASS | A remains canonical before owned restart; B activates afterward; API accounts/tokens remain instance-local. |
| WS-06 | PASS | Generic and trusted Workflow configuration save/reopen; invalid renderer claims remain errors. |
| APP-01 | FAIL | Actual route/startup proof exists; WC-C1/WC-C2/WC-C3 prevent shared-shell closure. |
| APP-02 | PASS | Defaults and downstream currency consumer; Secrets, Files and provider history owner/UI journeys. |
| APP-03 | PASS | API administration, current session/revocation/denial and deployment-policy controls. |
| APP-04 | PASS | Catalog/route persistence, actual Agent selection and backend allow-list authority. |
| APP-05 | PASS | Resources registry, promotion, governed reopen and actual file bytes. |
| APP-06 | FAIL | Real MAF approval/cancel/reopen controls pass; WC-C1 leaves shared Simple Chat lifetime open. |
| APP-07 | PASS | Actual immutable Workflow input/version, runtime, governed output and TestLab recording; external response scripted. |
| APP-08 | PASS | Actual one-shot Quartz delivery, file identity and owner replay producing one output. |
| APP-09 | PASS | Prompt CRUD/finalization and actual composer insertion with independent item/version/content read-back. |
| APP-10 | FAIL | Latest complete sequence reproduces shared scope/Dialog errors and disconnected browser-state persistence in its full log check. |
| APP-11 | PASS | WC-T2 reproduced with an unrelated real plan; repaired Reset compares cleared filters and exact owner inventory. Original global lifetime findings remain open. |
| APP-12 | PASS | Real packaged runtime invocation and shipped Office365 grant refusal before OAuth/network. |
| APP-13 | PASS | Supported Memory query/provenance; all unavailable-mutation and unsafe-URL assertions retained. |
| APP-14 | PASS | Linked CRM/HR people, agents, projects/tasks and persisted exact resource receipt. |
| APP-15 | FAIL | Context/receipt/retirement controls pass; WC-C1/WC-C2/WC-C3 remain open across shared host history. |
| LIVE-01 | FAIL | Live negative controls pass; no complete positive create/attach/metadata/content/reopen proof. |
| LIVE-02 | PASS | Actual live CRM planner and HR approve/deny, nonzero provider activity and persisted effects. |
| LIVE-03 | FAIL | Real Workflow requests occurred; no successful final live runtime asset/output proof. |
| GATE-01 | PASS | One frozen Release run: 15,849 passed, zero failures/skips across 19 projects; later narrow UI/test edits have explicit scoped revalidation. |
| GATE-02 | FAIL | Complete 143-case rerun: shared-host failures, twelve missing external prerequisites and one non-executed generated-app case. |
| GATE-03 | PASS | All eight evaluated graphs agree; protected graphs/watch inputs unchanged; probe bytes restored. |
| GATE-04 | PASS | Portability enforcement, documentation/package/evidence checks, reviewed secret findings and exact owned-resource cleanup. |

## Broad checkpoints and retained scopes

The explicit user/package requirement authorizes a new full Stable checkpoint after
Workspace completion. The final frozen build includes all runtime fixes, including
the Workflow token-limit handoff, at
`abfb2b368c24e06a107047aed3f84c3d72d2f33fb6d71ed0c4239a5d3ea88594`.
It uses Release, SDK 10.0.303/runtime 10.0.12, Windows, source-mode siblings and isolated
PostgreSQL 18.6. Both live opt-ins are false. Prior interrupted and failed Stable
attempts remain partial, with their own inventory/logs; they are not combined into a pass.

The full Stable result is **15,849 passed, zero failed, zero skipped**, with all 19 projects
reconciled against 15,794 discovered cases. Runtime theory expansion adds 34 Unit rows,
five Integration rows and 16 Memory rows. All seven theory identities and their concrete
rows are recorded; there are no missing or unexpected cases. Total elapsed time was
8,426 seconds (140.4 minutes), including product/test builds and discovery on this machine.
This local serial run does not establish the duration or result of the differently sharded CI jobs.

The subsequent WC-F6 fix changes only Data Sources UI initialization and its scenario hook.
All owner, runtime, contract, project/dependency and Stable fixture inputs are unchanged.
`data-sources-acquisition-revalidation-scope.json` lists every changed path and before/after
hash. The new leaf passes 21 cases, affected host/MainLayout/lifetime selection passes 14,
and the real two-database plus source/published sandbox selection passes seven.
The frozen Stable run is not described as compiling that later UI fix.

The final Release build also exposed unresolved `SectionHeader` markup in the two new
sandbox introductions. WC-F7 has four failing-before and four passing-after source/published
browser cases using actual BaseLib heading/text components. Every original sandbox
behavior assertion remains. `sandbox-heading-retained-scope.json` confirms that only the
two sandbox pages and their visibility assertions changed; production and dependency
inputs remain valid under the preceding scopes.

The final current-source Release product build passes with zero warnings and zero errors.
The recorded development-loop samples belong to their stage checkpoints; no new timing
measurement is claimed after WC-F6/WC-F7. All nine probed source files remain restored,
and the four protected watch input sets are unchanged.

The original complete browser run reconciled all 142 cases. Runner output was 123 passed
and 19 failed; semantic accounting was **122 PASS, 7 FAIL, 12 BLOCKED, 1 NOT_RUN**.
Older xUnit dynamic-skip prerequisites appear as runner failures. Scenario04 returns
before browser creation without its URL/root; its runner pass is not execution proof.
Focused harness repairs and isolated owner reruns are preserved separately.

A second broad browser checkpoint was triggered by the changed shared Playwright host
retirement fixture, stronger Memory/Prompt/defaults observations, the added retirement case
and WC-F6. At checkpoint
`15e26ac35fdd97478587e21af118fe66fce53bd6b019af9b11053810499b4a3c`,
it reconciled all 143 cases in 1,422 seconds. Runner output was 129 passed and 14 failed;
semantic accounting was **128 PASS, 2 FAIL, 12 BLOCKED, 1 NOT_RUN**. Collaboration's
complete log check reproduced WC-C1/WC-C2 and exposed WC-C3. TestLab exposed an invalid
global count assertion after CRM had saved a plan into the shared private database.

WC-T2's controlled unrelated-plan seed failed with the original assertion. The repaired
one-case test passes every original owner/history/log assertion plus cleared filters,
exact owner count and visible rows. Production and shared fixture bytes are unchanged;
`testlab-reset-retained-scope.json` records that comparison. Effective accounting is
**129 PASS, 1 FAIL, 12 BLOCKED, 1 NOT_RUN** across the broad run and this scoped repair.
That combination is explicitly not a clean broad-run result. Live flags stayed off;
these browser gates never restarted the bounded live campaign.

## Live usage and follow-up boundaries

The campaign used the configured **OpenAI Responses / gpt-5.4-mini** provider and exactly
**40 of 40 authorised outbound model requests**, counting failed attempts and retries.
No execution exceeded ten, no cap was raised, and no new provider
feature or broad authority was introduced. The two initial CRM cases' no-send rehearsal
used zero requests and is recorded as REHEARSAL, separately from their actual live pass.

The real CRM planner and HR approve/deny journeys passed. The live negative Project
Structure case proved sibling-read refusal, rejected attachment, unchanged nodes and
unchanged denied content. The positive case has partial real write progress but lacks
complete attachment and both independent read-backs. The final attempt also exposed a
test-host retirement/evidence defect; its deterministic repair passes without claiming
to repair missing live proof.

The real Workflow first rejected an unsupported temperature parameter, then returned
HTTP success with a failed provider journal and failed Workflow run. The exact response
diagnostic was not retained, so an incomplete response under the output cap remains a
hypothesis. Scripted-boundary positive runtime proof is separate from this live failure.

Follow-up records provide the original identities, timeline, call/owner maps, competing
causes, attribution confidence and acceptance boundaries:

- [WC-C1: scoped Simple Chat reads during retirement](workspace-shared-shell-lifetime-finding.md).
- [WC-C2: shared Dialog cancellation during disposal](workspace-dialog-retirement-finding.md).
- [WC-C3: browser tab-state persistence during circuit retirement](workspace-browser-state-retirement-finding.md).
- [WC-L1/WC-L2: complete live file and Workflow proof](workspace-live-follow-up.md).

No suppression, quarantine, automatic mutation replay, hot database switch, schema change
or new provider capability was used to make a gate pass. A separate authorised live budget
is required for further live attempts. WC-H1 records temporary test-fixture maintenance;
it is distinct from these application blockers.

## Closure review and environment

This sealed package has a compatible flat shape: its prompt/review/foundation are inputs,
phase documents own requirements, the completion map owns the boundary plan, and the
campaign plan/results plus maintained records own proof and closure. The canonical skill
folder validator is therefore not a reason to restructure or modify the sealed package.
Its own package validator and campaign hash validator are used alongside manual semantic
review; neither tool can certify runtime truth.

Raw-input closure is **Solved** for the three remaining Workspace boundaries, independent
sandboxes, authority preservation and bounded repairs. It is **Partially solved** for full
application readiness: real campaigns executed, but the explicitly mapped failed and
blocked cases remain. The requested complex-finding maps are delivered without speculative
cross-owner or shared-library changes. The implementation does not proceed to another
module extraction.

Final no-write portability enforcement passes with 15,214 reviewed executable-source
findings unchanged. The package remains sealed and unchanged. Source/evidence text
findings are reviewed synthetic test sentinels or unevaluated expressions; a raw scanner
result containing findings is not represented as a clean scan. Current trace text members
and selected screenshot/trace images have separate inspection records, with explicit
limits on unviewed binary content. Raw logs and media remain private and ignored.

The exact task-owned PostgreSQL container, its anonymous volume, port 63343 and private
credential root were removed after the executions. All 12 recorded watch processes, ports
and roots are absent. Ordinary port 5032, retained application data and unrelated sibling
resources were not targeted. Sibling revisions and pre-existing modifications are unchanged.
The attempt index retains 134 raw TRX executions and 39 discovery-only/aborted/build-failed
records; those attempts are not summed into unique coverage or a fabricated clean run.

All 29 required groups have a final disposition. Campaign bookkeeping validates evidence
hashes and the 40-request total; complete-campaign enforcement remains deliberately failed
for APP-01, APP-06, APP-10, APP-15, LIVE-01, LIVE-03 and GATE-02. This is the recorded
application readiness decision, not a waived gate.

Older media copied into an ignored evidence snapshot remain excluded from current proof.
Automatic approval review rejected their attempted removal with “blocked by policy”.
The original files were preserved; current-run inventories identify accepted media by
producing run, timestamp and hash. No deletion retry was used to bypass that decision.
