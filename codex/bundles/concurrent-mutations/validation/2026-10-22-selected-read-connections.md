# Selected authority read connections and receipt recovery

Status: focused qualification complete. Producer activation and complete bundle closure
remain gated by the root lifecycle, refresh, remaining integrations and capacity work.

## Change and ownership

Process persistence owns one fresh connection for each original-receipt or selected-
dispatch authority read. The private shared wrapper closes it in finally on success,
refusal, cancellation and failure. Each check still executes its four existing queries
and observes changes committed between them. It starts no transaction and caches no
authorization. Generic dispatch/reservation and caller-owned mutation transactions are
unchanged. The supported test-only InMemory path remains valid.

Core adds two optional telemetry spans around inherited run detail and current authority.
The HTTP diagnostic probe measures connection opening separately from SQL execution.
No new runtime type, public contract, project reference, partial class or registration
was added. The existing persistence class gains one private helper shared by two callers.
The connection spans bounded SQL reads and source validation, without file/provider/UI
waits. Holding a pool slot between queries remains a sustained-capacity consideration.

## Correctness and regression proof

All 235 selected executions pass: 234 distinct cases, with the receipt-recovery case
also run separately before the full outcome-reader class. Eighteen new cases prove both
snapshot modes, one fresh connection per check, closure on all exits, no transaction or
writes, changes committed between queries and nonrelational compatibility.

| Selection | Discovered / passed |
| --- | --- |
| ProcessOriginalAuthorityConnectionTests | 18 |
| Recovered_effect_receipt_does_not_close_work_interrupted_by_lost_acknowledgement | 1 |
| Profile_HTTP_review_through_existing_telemetry | 3 |
| Warm_HTTP_review_records_complete_read_cost_at_each_accepted_plan_size | 6 |
| ProcessDecisionOriginalSourceTests | 30 |
| ProcessDecisionDispatchAuthorityTests | 40 |
| ProcessInheritedSourcePolicyTests | 15 |
| Integration-ProjectTaskPlanProcessOutcomeReaderTests | 14 |
| Integration-ProjectTaskPlanProcessChildStopperTests | 10 |
| Integration-FileSandboxWorkspaceProcessQueryIntegrationTests | 12 |
| Integration-AgentToolBackgroundJournalIntegrationTests | 35 |
| Integration-AgentInheritedPlanningSavedEffectTests | 51 |

The earlier parent-stop checkpoint's 65 unaffected Unit/direct-file cases remain valid;
122 adapter/outcome/remaining regression cases are rerun here. This closes focused
qualification for all 187 selected parent-stop cases, including all 58 new parent-stop
behaviors. It does not assert that all 187 were rerun in this candidate.

The receipt-recovery deadline remains two seconds. One baseline failure and its three
isolated failing repeats remain in the earlier signed proof. Recovery now passes both
its isolated selection and the complete outcome-reader class. No deadline was raised.

## Measured read cost

The same isolated PostgreSQL fixture retains pooling=false. Every HTTP read still
executes 27 Process SQL statements; connection opens fall from 22 to 10. The three
diagnostic shapes use two warmups and five measured reads each:

| Shape | Before median ms | After median ms | Before / after connections |
| --- | ---: | ---: | ---: |
| RunSnapshot / 1 | 778.21 | 680.39 | 22 / 10 |
| StepSnapshot / 1 | 794.96 | 711.66 | 22 / 10 |
| StepSnapshot / 1024 | 836.25 | 589.96 | 22 / 10 |

The separate finite-reference gate passes all six shapes and all 30 measured reads
under the unchanged one-second limit:

| Mode / steps | Median ms | Maximum ms |
| --- | ---: | ---: |
| RunSnapshot / 1 | 745.03 | 762.36 |
| StepSnapshot / 1 | 591.79 | 780.95 |
| RunSnapshot / 128 | 593.38 | 714.97 |
| StepSnapshot / 128 | 866.28 | 923.59 |
| RunSnapshot / 1024 | 682.25 | 726.82 |
| StepSnapshot / 1024 | 741.67 | 818.47 |

Diagnostic activity spans overlap and must not be summed. Process allocation includes
all activity in the test process. Host-load observations are retained with their
visibility limits. These finite local measurements are not sustained production capacity
or pooled-driver performance guarantees.

The intermediate original-receipt-only change passed 18 executions and failed one
finite shape at 1004.32 ms; 167 cases were not run. Its nine new cases and receipt
recovery passed. This candidate extends the same lifetime ownership to the remaining
selected-dispatch path. The earlier missing test namespace import and zero-test filter
namespace error are retained as rejected build/discovery attempts, not passing evidence.

## Build, architecture and static checks

Direct Release Persistence and Web builds pass, followed by the refreshed Integration
assembly. The Core telemetry build also passed in the baseline; that source is unchanged.
All builds use standard outputs and local sibling-library references. Scoped architecture
comparison adds no dependency edge or cycle. Core retains its three existing cycles;
Persistence remains acyclic. The existing query-type size finding reflects the one
private helper. Ten existing WS0002 package-advisory workspace diagnostics are retained
separately from successful builds.

Full portability enforcement passes with 15665 reviewed findings unchanged. Rendering
review passes for 1212 current inputs. Documentation and whitespace checks pass. Validator
tooling tests passed in the preceding candidate on unchanged tooling. No baseline was
rewritten to hide new findings.

Frozen source SHA256: `9EC2951EBD942B46269DE26B8451EA7356EAFCFE8BA5506B3E28F6A647BE77B7`.
Frozen runtime SHA256: `373B59C518E57FD9B5E55B6A721FBF5C88C7935005F52F80D963CF81FD07BA55`.
Source/runtime identity verification passes before documentation-only closure edits.

## Evidence and resources

[Compact proof](../proof/2026-10-19-planning-authority/c4-selected-read-connections.json)
retains all three candidates' commands, discovery/results, measurements, architecture,
input identities and resource reconciliation. Its SHA256 is `BF70D3E814938B25C2B89B84D2244FBCF43EBD807AB7CA24BC309332AEBA0D17`.
The proof passed the secret scanner, including the private fixture-password sentinel.

All test commands are covered by the owned resource observer. Each run leaves zero new
observed processes, database names or temporary paths. The existing isolated PostgreSQL
server remains needed for subsequent lifecycle work. Ordinary 5032 and installed tooling
are preserved. Historical cleanup rejections remain recorded in [resources](../resources.md);
this checkpoint does not claim those resources were removed.

## Next gate

Proceed with persisted root quiescence and new-work admission fences, then durable
recovery and snapshot refresh. Producer/Workflow/settings integration, remaining writer
waves, sustained capacity and final integrated/Stable proof remain open. SPA stays on
its own `codex/concurrent-mutations` worktree; no merge, push or colleague contact occurs.
