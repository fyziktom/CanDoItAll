# Chat-session publication and generated-client integration

P20 qualifies focused session creation and conditional title changes. Core owns the pure
title condition; Persistence applies it after acquiring the selected agent/session keys.
Shared index changes from unrelated executions no longer invalidate session creation.
Rename preserves the current transcript and compatibility state, and late execution
outcomes preserve the current title. Catalog, selected identity and pending-publication
barriers remain in force. Process manager chat now creates its initial title in one write.
See the [design](../plan/P20-chat-session-publication.md) and
[public contract](../../../../docs/agent-chat-concurrency.md).

HTTP requires the displayed original title: missing expectation is 428, stale title is
409, and invalid identity/title is 400. Native hosts retain their selection/lifetime
guards and failed drafts. No automatic conflict retry or new workspace-wide long lock
is introduced. This does not create durable manager-chat identity across two browsers.

## Backend proof

There are **365 distinct passing cases** on unchanged P20 production code. The first run
executed 365, passing 348 and failing 17. Five test files then received explicit fixture
repairs, and the affected five topics passed **260/260**. The remaining 105 passing cases
are from the original candidate, not a claimed clean aggregate rerun.

| Topic | Cases |
|---|---:|
| Pure title condition | 10 |
| Native selected-key publication races | 10 |
| Title HTTP contract and public projection | 9 |
| Admission read scaling | 15 |
| File lock and catalog races | 17 |
| Process selection index | 23 |
| Publication recovery and validation | 13 |
| Agent chat effect ownership and sessions | 60 |
| Process workspace shell | 187 |
| Contextual failure retention | 17 |
| Actual Agent and Process browser scenarios | 4 |

The fixture repairs account for seeded default sessions, compare compatibility values
structurally, register the existing cancellation client in shell fixtures, and require the
precise catalog-witness conflict. Read-scaling tests now separately permit one bounded
compact Process index read introduced by the earlier index reuse change; historical
run/usage reads remain forbidden and the other original budgets remain unchanged.
The actual `Editable` title callback proves retention of the proposed title after refusal.

Fourteen direct Release builds passed, followed by the two affected test builds after
fixture repair. Both candidates retain **825 source pins and 106 output identities**;
the exact five-test-file bridge records the production-unchanged boundary. Initial
discovery omitted a partial shell test file; corrected discovery found all 187 shell
cases before execution. The failed discovery and all failed executions remain retained.

Full portability enforcement passes with **15,688 reviewed executable findings unchanged**.
Rendering-register validation passes for 1,212 inputs. Portability, secret-scanner and
rendering-tool tests pass. The initial observer covered 15 commands for 539.24 seconds;
the repair observer covered five commands for 192.66 seconds. Reconciliation finds zero
new processes, databases or temporary fixture entries in either test run.

## SPA integration on its own branch

SPA remains on `codex/concurrent-mutations`. The initial API fixture omitted optional
access-management and Runtime routes. The corrected isolated export maps the real Runtime
endpoints and enables user authentication/access management without querying user data.
It contains 317 routes, is 4,522,796 bytes, and has SHA-256
`cf037d185e41579acd72a95e52af0db2cc6f71d1793ab16748bc4cd0ec82ee3a`.
The earlier partial export and importer/build failures remain evidence.

Generator repairs preserve inline discriminated schemas under deterministic names, HTTP
status/body combinations, cast field access, required discriminants and nullable values.
Generated files are regenerated from the reviewed input. No response variant or protected
Projects shape is replaced with a permissive fallback. Actual serializer and SDK tests
prove exact original conditions, nullable policy, conflict bodies and a single attempt
even for HTTP 503 with retry headers. Generation, **25 generator tests, four generated
client tests, 29 app tests**, owned formatting/lint, full typecheck and production build
pass. The app's additional Vitest lane has no tests; it is not counted as coverage.

The Workspace defaults page uses a small state owner and the existing bounded transport.
It copies the original expectation and six values, preserves the hidden provider value,
disables editing during save, installs exact acceptance and retains drafts after known
refusal or unknown acknowledgement. Retry is blocked until explicit discard/reload;
a current read is not presented as proof of an earlier uncertain save. Existing Project
deletion refusal now stays an error instead of being cast to success. Its lifecycle
producer, other remaining SPA producers and durable recovery remain separate work.

The actual compiled SPA passed a controlled HTTP browser scenario at **1920x1080**:
normal, saving, accepted, conflicted and unconfirmed states. It made exactly three writes
and two reads, the second read only after explicit discard/reload. The form retains the
failed draft; the Save action is disabled after uncertainty. Inspected normal/unconfirmed
screenshots show readable labels/actions and no horizontal overflow or clipping; the
unconfirmed form ends at y=1056 in the 1080-pixel viewport. No new overlay was introduced.
This is rendered frontend proof with controlled responses, alongside separate real backend
HTTP/file/browser proof; it is not a claimed live SPA-to-backend end-to-end test.

## Architecture, artifacts and progression

Core/Persistence/Components scoped comparisons preserve existing cycles and add only one
reviewed informational member-count finding on the cohesive title-change value. Final
Web and product-module observations retain existing large-owner findings and the two
known AgentFramework namespace/type cycles. Earlier Web/module snapshots use different
request/generated-source scopes, so their raw total counts are not comparable deltas.
Actual project manifests and references are unchanged; no project, interface or business
partial is added. CodeAnalytics diagnostics remain recorded rather than treated as a
fully resolved project-reference graph.

Both API export fixtures disposed their isolated roots, and their resource reconciliations
pass. The SPA browser context closed and its owned preview exited; standard build outputs
are retained for reuse. The first observer copy lacked a deadline helper and failed before
export; the first preview used an incorrect installed Vite path and exited before hosting;
the first browser script could not import filesystem APIs and failed before opening its
owned context. Each was corrected without replacing or touching ordinary port 5032.

Artifact scans find only the reviewed three PowerShell variable expressions in each test
run; the private fixture password is absent, and no oversized/unreadable text was skipped.
Separate guarded removal of the new portability JSON and CSV was rejected by automatic
approval review with `blocked by policy`. Both files, totaling **50,588,390 bytes**, remain
with hashes and ownership recorded. Historical denied targets were not retried. Whole-bundle
cleanup is therefore still incomplete.

Proof roots are `c4-chat-session-publication`, `c4-chat-session-publication-repair`,
`c4-chat-session-schema` and `c4-chat-session-schema-full` under the owned validation root.
SPA screenshots are in `output/playwright/concurrent-mutations-spa-p20-20261023`.
The compact local proof is
`codex/bundles/concurrent-mutations/proof/2026-10-23-chat-session-publication.json`.

Behavioral closure: **Pass with the recorded cleanup blocker**. Continue with capacity and
worker activation, durable same-source manager identity, remaining mutable owners/policies
and final integrated E2E. Broad Stable remains reserved for that final frozen candidate.
SPA requires colleague review before any main integration; no merge or push was performed.
