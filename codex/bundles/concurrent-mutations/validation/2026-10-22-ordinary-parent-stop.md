# Guarded ordinary Agent parent stopping

Status: Behavioral prerequisite passes, 112/112 selected cases. Process adapter/stages,
capacity, producer activation and the whole bundle remain open. SPA stays on
`codex/concurrent-mutations`; no SPA main merge is authorized.

Core now owns one internal parent-stop protocol: validate original/current authority,
signal only an exact admitted session, acquire the dispatch lease with a 250 ms wait
budget, reread, then use guarded owner publication. Ordinary and inherited planning
journals keep separate evidence policies. The protocol has no Process, SQL or UI
dependency and invokes no provider, effect or full execution runtime. Existing registry
APIs remain available; parent recovery uses the new exact-session overload so a reused
execution GUID cannot signal a different source/profile generation.

The ordinary policy preserves original inputs and provider responses while cancelling
only undispatched proposals through the existing journal transition. Started/unknown
provider or effect work remains unresolved; committed unfinished effects remain with
their completion owner. Legacy invocation without durable provider evidence cannot
become closed merely because an old observer forced terminal status. Request-scoped
attachments bypass the MAF durable invocation context and also remain unresolved after
loss of the live registration, even if their stored status is terminal. This is explicit
pending work, not automatic reconciliation or guessed cancellation success.

| Selected class | Project / expected / discovered / passed |
| --- | --- |
| AgentBackgroundParentStopTests | Unit / 37 / 37 / 37 |
| AgentInheritedParentStopTests | Unit / 20 / 20 / 20 |
| AgentBackgroundExecutionIdentityTests | Unit / 15 / 15 / 15 |
| AgentExecutionCancellationRegistryTests | Unit / 20 / 20 / 20 |
| AgentBackgroundParentStopTests | Integration / 11 / 11 / 11 |
| AgentInheritedParentStopTests | Integration / 9 / 9 / 9 |

Each filter is `FullyQualifiedName~` followed by the exact class prefix and a trailing
dot. Integration classes use `CanDoItAll.Tests.Integration.Runtime`; Unit uses
`CanDoItAll.Tests.Unit`, with `.AgentFramework` for the registry class. Proof covers
authority loss before signal/after lease/at publication, original-binding replacement,
foreign registrations, live signalling, caller cancellation, lease contention, late
provider dispatch, unknown effects, committed completion, terminal replay, and lost
publication acknowledgement. The composition test resolves the actual Agent module
factory over an isolated file-store owner and shared registry across scopes; it does
not claim an HTTP-host test. Existing inherited accounting/work semantics still pass.

Core, Workbench, Agent module, Web, Unit and Integration built in that dependency order
with standard Release outputs and `--no-restore --no-dependencies
-p:UseLocalCanDoItAllLibraries=true /m:1`. Twenty owning/copied DLL/PDB identities and all
source pins match before/after testing. Two earlier compile failures were fixture errors:
an internal enum in a public theory signature and an attempted assignment to an immutable
session-reference property. Both are retained. The first 107-case candidate passed but
review found its terminal legacy replay gap; it is superseded. The 109-case intermediate
build passed without test execution before request-scoped review added three cases.
Retained proof includes 219 test executions (107 earlier, 112 final), all passing against
their respective sources; only the 112-case candidate qualifies the final source.

CodeAnalytics reports 799 to 802 types and 5918 to 5931 members, with the same 228 existing
findings, three unchanged existing type cycles and no diagnostics or findings on the new
owners. There is no new project reference or partial file. Rendering registration review
passes all 1212 inputs. Full portability scanning includes new untracked source and final
no-write enforcement passes with 15670 reviewed findings unchanged. Validation-tool tests
pass 6/4/7. Documentation and whitespace gates pass. Broad Stable is reserved for the
named final integrated checkpoint, not claimed here.

The compact proof is `proof/2026-10-22-ordinary-parent-stop.json` under the bundle root.
Both candidates' twelve execution commands are fully observed and have zero new process,
database-name or temporary-path residue. Historical policy-denied cleanup remains open
and was not retried. The shared isolated fixture is retained for the next integration.

Next bind stopping to the original Process receipt, exact root and stable current profile
generation, then connect bounded runtime/decision/shared-scope/quiescence recovery. Keep
notification cursors and sticky pending-cycle state across takeover. The earlier compact
index's cost regression, bounded waits/fairness, snapshot refresh, Workflow/settings,
remaining writer coverage and final Stable/E2E proof remain explicit open requirements.
