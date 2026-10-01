# Review of Projects P1 and entry decision

## Decision

Preserve P1 and proceed to Files P2 after the bounded P1-R1 and P1-R2 repairs. The shared foundation and current canonical seams remain appropriate. No new critical Workspace defect was identified in the paths inspected by this review. This is not an all-application no-regression certificate.

Main reviewed source is `c207b499c3165ba95e46ffaf518884097d3135dd`, three commits after the previous reviewed head. The change inventory separates archived P1 instructions, S0 test work and product implementation. Components development remains the published `4a858412d2c2a3f6123bf23d8c4584f05b47627d`; no repeat publication is required for that ref.

## Preserve

The real board, cards, hierarchy and five-step editor moved into Projects.UI; mixed EF/application types were separated into existing Projects.Contracts. Files is an explicitly host-supplied slot rather than a transitive module dependency. The P1 leaf declares BaseLib plus Projects.Contracts, and the new negative dependency tests reject forbidden and unresolved edges (R02, R03, R22, R23).

The native SaveEditor acknowledgement contains actual project/lifetime and child IDs. A stable draft merges unchanged submitted fields while preserving later edits and original live-row identity. Unblurred text and mounted date inputs retain real form state. Admitted seeding reaches the existing native writer with the original lifetime; legacy ID-only callers were not silently migrated (R04-R10).

Workspace's prior deletion/navigation repairs and P1's two-pixel Files geometry correction remain part of the baseline, not a request to reimplement them. Verify their relevant consumers, not the whole history of the campaign by default.

## Bounded follow-up

P1-R1 is a source-confirmed classification/recovery gap in the page: known original-lifetime refusals enter generic unknown handling. The existing seed regression verifies the safe backend rejection but not the incorrect UI state or its per-ID lock on a newly acquired lifetime. See [S0](S0_KNOWN_REFUSAL.md). The author did not run a new C# reproduction.

P1-R2 is an additional exact path in the inspected modal: failed module import is caught in Save, but closing re-awaits the same faulted task in Dispose and does not catch that ordinary JSException. This can break the very Close/reopen recovery the UI recommends. See [JS lifetime repair](S0_FORM_JS_LIFETIME.md). Keep active validation and lifetime controls; no general claim is made that every possible interop teardown is broken.

## Test evidence, not reviewer execution

The following are the implementer's recorded results (R01, R02), not reruns by this review:

| Scope | Recorded result | Limit |
|---|---|---|
| Exact cold shared-provider S0 journey | 1 pass; 29 component/session controls pass | Historical selector cause remains unresolved; production Simple Chat code unchanged |
| Final Projects page/native mutation tests | 37 pass | The 11 Files owner cases are a subset, not 11 extra unique tests |
| Leaf form/boundary tests and context/CRM consumers | 11 and 6 pass | Specific layer proof only |
| Agent file and Workflow/TestLab consumers | Successful later recorded journeys | Earlier deadline and test-readiness failures remain; no causal repair claimed for two refusal deadlines |
| Full frozen Stable | 15,906 executed: 15,905 pass, one failure, zero skips | 15,851 discovery entries plus 55 expanded theory rows; failed scanner found synthetic negative-control arguments in a generated discovery artifact |
| Complete owning Unit rerun | 9,400 pass on the same frozen binaries | Closes the artifact repair; does not retroactively make the full Stable run all-green |
| Final focused source verification | Recorded current binary/PDB comparison and direct builds | Some final host, sandbox and Files alignment changes postdate the broad checkpoint |

The scanner was not weakened. Raw discovery was retained under the established private artifact policy; a shareable derivative normalized three known synthetic arguments. No real credential exposure is inferred from that scanner failure. Private TRX, raw logs, screenshots and runtime snapshots were not supplied to this reviewer.

## Development-loop interpretation

The report records five projects in the P1 renderer closure and six in its sandbox; watch sets are 4,585 original Web / 4,597 extracted Web / 300 sandbox. That is strong structural isolation, not proof every edit is faster. In particular sandbox Razor samples 54.260 / 14.235 / 13.652 seconds were not consistently faster than the Web samples; sandbox C# and scoped CSS samples were much shorter. Concurrent validation, hydration and small sample sizes are explicit confounders (R02).

P2 must not destroy that isolation by adding file integration to the P1 leaf. Measure its own comparable development loop; do not repeat the earlier numbers as current benchmarks.

## Qualification retained

The historical S0 selector anomaly is `REVALIDATED_WITHOUT_ROOT_CAUSE`, not fixed. Reopen on recurrence or actual implicated changes. Unavailable paid/live tests, generated-app fixture and future consumer CI are separate prerequisites, not successful proof. This package author performed source review and package checks only; no .NET build or browser execution.

The final branch recheck remained at the reviewed SHA. GitHub Actions returned zero runs for it; this is not evidence that the recorded local tests were not executed.
