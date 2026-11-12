# Legacy root cancellation cutover

Root cancellation now has one product admission path. The legacy Application method
refuses a root before any command, observer or projection; HTTP maps that refusal to
409 with a stable migration code and the finite cancellation Location. Selected-child
cancellation keeps its existing behavior. The synchronous descendant cascade, fixed-pass
retry loop, hierarchy dependency and duplicate HTTP projection catchup are removed.
See [the API contract](../../../../docs/process-cancellation-api.md).

## Real producer repair

The initial 56-case run passed 55 cases. The real MAF provider-wait cancellation failed
its 60-second deadline. A one-case diagnostic on unchanged production reproduced the
failure: the selected original Agent was signalled, then recovery remained at
`UnresolvedEffects`. Ordinary model requests with local function proposals did not have
a hosted-tool dispatch record, so recovery could not distinguish them from historical
invocations missing original evidence. Increasing the deadline would not fix that.

New MAF invocation checkpoints now persist an immutable typed provider effect scope.
An invocation restricted to local proposals rejects added hosted tools before the wire.
The Core stopper can close that invocation after acquiring its original dispatch lease
and checking current Process authority, provided no local effect remains unresolved.
Historical unknown scope and unacknowledged hosted dispatches remain unresolved. A
terminal run status cannot manufacture the missing evidence. This contract does not
claim that a remote model request stopped or incurred no usage.

Models validates the scope and immutable checkpoint transitions; MAF classifies and
enforces its provider contract; Core interprets the saved evidence. There is no new
project, reference, interface or business partial. The scope is included in the existing
checkpoint write, adding no durable publication per provider request. Provider-only
retry behavior and the existing hosted-tool uncertainty protocol are preserved.

## Validation

The final candidate passes all **173 discovered and executed cases** in 11 topics:

| Topic | Cases |
|---|---:|
| Legacy Application operator and persisted recovery | 33 |
| Legacy HTTP root refusal and selected-child behavior | 3 |
| Real MAF Process scenarios | 6 |
| Native client, finite HTTP admission and registered worker | 14 |
| Independent background-stop owner | 45 |
| Journal schema, original scope and historical wire compatibility | 33 |
| Hosted-tool journal and contract-drift rejection | 12 |
| Native provider protocol, replay and retained historical journals | 27 |

The provider-wait case passes in 13.76 seconds including fixture setup and disposal,
with its original 60-second cancellation budget unchanged. It requires root and Agent
cancellation, zero tool effects and exactly one wire request even after another dispatch
attempt. Hosted failure tests still prohibit redispatch after an uncertain remote effect.
The final snapshot retains 509 source pins and 66 owning/copied binary identities,
verified unchanged after execution. Eleven direct affected Release builds passed.

Full portability enforcement passes with 15,688 reviewed executable-source findings.
The intentional delta is one Process HTTP Location string matched by the `/proc`
heuristic and four removed obsolete XML route-comment matches. The provider-scope repair
adds no further baseline delta. Rendering-register validation passes for 1,212 inputs;
validation-tool tests, documentation and whitespace checks pass. CodeAnalytics finds no
added findings or changed project references/cycles. Three pre-existing Core type cycles,
one MAF type cycle and collector diagnostics remain unchanged. The Application owner loses
its large-file finding after the cascade removal.

The first observer launch failed before tests because the ownership record omitted its
required state field. Its original pre-creation record and failed command are retained;
the corrected observer covered all 11 test commands for 413.82 seconds. Reconciliation
finds zero new processes, databases or temporary fixture entries. Four new bulk scan
files totaling 101,148,878 bytes were removed after preserving hashes, summaries, baseline
review and final enforcement. Historical denied cleanup targets were not retried.
Artifact scans find only the three reviewed PowerShell variable expressions per run;
the private fixture password is absent, with no oversized or unreadable text skipped.

The three run directories are `c4-legacy-cancellation`,
`c4-legacy-cancellation-diagnostic` and `c4-legacy-cancellation-repair` under the owned
`.artifacts/concurrent-mutations/validation` root. The ignored compact local proof is
`codex/bundles/concurrent-mutations/proof/2026-10-23-legacy-cancellation-cutover.json`.
Both failed candidates remain recorded; they are not counted as passing runs.

## Progression

P19 is qualified. Recovery remains disabled by default pending capacity/deployment
qualification. Manager-chat create/rename concurrency, remaining mutable owners and
policy settings, and the integrated final E2E gate remain open. Broad Stable is reserved
for that frozen integrated candidate. SPA remains on its dedicated branch for colleague
review; this checkpoint makes no SPA change and does not merge or push it.
