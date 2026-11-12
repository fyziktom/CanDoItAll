# Inherited review reads and isolated catalog cost

The read boundary is implemented; capacity is not qualified. All 67 selected
correctness cases pass on the production candidate. The final diagnostic selection
passes three component cases and fails all six HTTP latency cases after complete
sampling and safety checks. This checkpoint does not close the E2E request.

## Change and responsibility

Core now shares inherited journal disclosure and unique participant selection through
`AgentInheritedPlanningReadAuthority`. The existing coordinator delegates to it.
`AgentInheritedPlanningRunReader` reads the actual retained detail through the existing
store and current disclosure ports, without an execution runtime. The Agent module
registers it with the existing scoped store, profile, generation, policies and clock.
Workbench uses it for both initial review and final child-revision observation.
Manager/source/profile/Project checks, original intent bytes and HTTP mappings remain.
There is no new project reference, singleton or authorization-decision cache.

## Proof and corrections

- Direct standard Release builds passed for Core, Workbench, Agent module and Web,
  then Unit and Integration outputs. Test-only changes refreshed Integration again.
- Eight isolated disclosure cases, seven file-store reader cases, 19 coordinator
  cases, 26 manager-review cases and seven real HTTP cases passed: 67 correctness
  cases. They cover both snapshot modes, denied/current authority, exact retained
  detail, missing/ambiguous participants, restart and uncertain accounting responses.
- The first reader fixture assumed separately persisted approval records. Its 13/15
  result is retained; the correction explicitly persisted the approval through the
  existing synchronizer/store. Only that test source and Integration DLL/PDB changed.
  All 73 selected cases were repeated: 70 passed and three cost cases failed.
- That cost harness also rejected a background claim-recovery factory and counted
  unrelated SQL. Its 28 samples remain negative evidence. The final harness scopes
  factory prohibition, SQL and optional telemetry to the selected GET, and disables
  Process recovery only in cost hosts through the existing option. Production
  recovery and the seven HTTP correctness tests remain unchanged.
- The final selection has three passing component cases and six latency failures.
  Every cost case completed two warmups and five samples, constant 27 Process SQL
  commands, exact observations, one provider invocation, unchanged child revision
  and zero Project writes. The GET did not construct the execution factory.

## Isolated warm HTTP baseline

Each row has five observations. Allocation is whole-test-process allocation; this
is a single-reader reference with Process recovery disabled, not sustained capacity.
Do not interpret differences from the contaminated run as a production speedup.

| Mode | Steps | Median ms | Maximum ms | Median allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| RunSnapshot | 1 | 1354.71 | 1520.33 | 79,039,240 |
| RunSnapshot | 128 | 969.53 | 1256.34 | 79,369,904 |
| RunSnapshot | 1024 | 1051.60 | 1084.21 | 81,363,232 |
| StepSnapshot | 1 | 937.31 | 1033.44 | 79,070,632 |
| StepSnapshot | 128 | 1027.17 | 1261.48 | 79,331,080 |
| StepSnapshot | 1024 | 954.99 | 1129.91 | 81,365,488 |

For the 31-Agent, 734,165-byte default catalog, synchronous median component costs
are 92.33 ms / 5,925,256 allocated bytes for seed construction, 238.68 ms /
13,130,488 bytes for normalization, and 26.94 ms / 2,954,928 bytes for equivalent
catalog serialization comparison. Normalization includes seed construction. These
overlapping observations cannot be summed as disjoint request cost. They justify
designing bounded reuse of unchanged inputs while preserving current-file semantics.

## C# Architecture Gate Result

Status: Pass with follow-up for the read boundary; capacity remains blocked.

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Blocking for activation | Fixed catalog cost remains substantial | Six isolated latency failures and component allocation measurements | Design and qualify bounded current-input reuse; repeat the identical matrix |
| Follow-up | Production recovery can construct the execution graph while polling | Retained worker stack from the contaminated run | Include background work in later sustained capacity proof |

### Dependency direction

Core/Workbench snapshots `snap-20261021185512-26594287` and
`snap-20261021191541-26594287` retain nine existing scoped cycles and 628 findings;
the Agent-module snapshots `snap-20261021185717-99e80758` and
`snap-20261021191922-99e80758` retain two existing scoped cycles and 220 findings.
No project edges or cycles were added. The Workbench reader's execution-facade edge
was replaced by the narrow Core reader. Existing workspace diagnostics were reviewed;
successful direct builds provide compilation evidence.

### Partial-class policy

No partial class was added. Disclosure moved to one cohesive Core owner; the
coordinator shrank from 118 to 106 source lines in the inspected declaration range.
The two small new owners have four and three members respectively. Composition
remains in the existing Agent-module registration.

### Testability proof

The eight isolated cases use existing store/policy/participant ports and cannot call
execution or mutation methods. Seven real-store cases verify retained data and fresh
disclosure. The actual HTTP cost host rejects execution construction in the selected
GET. The reviewed rendering-register hash and rationale reflect the scoped DI change.

### Closure decision

Proceed only with the catalog-cost repair. Parent accounting recovery, refresh/cancel,
executor activation, Workflow/settings/UI, remaining writers, sustained concurrency
and broad Stable closure remain open. SPA is clean on `codex/concurrent-mutations`
and was not merged or pushed.

## Static, identity and resources

Full portability enforcement passes with all 15,664 reviewed executable-source
findings unchanged; no baseline was rewritten. Rendering, documentation links and
the validation-tool tests pass. The final resource-log EOF whitespace correction is
part of the post-run documentation-only bridge and final `git diff --check` enforcement.

The final frozen source contains 9,891 inputs, SHA-256
`A8779146069F546487DF31FC801D81FB3FBD78D44F67D1B7AECDA219D6B26098`.
Its 6,655 runtime files hash to
`FF49AAD6325D4373C4D9391FB053388BCAB2D3C5798DA3A77566C33F010A42E3`.
Both were verified unchanged after execution. The exact bridge from the preceding
candidate changes only two test sources and two documentation files, plus Integration
DLL/PDB; production source and runtime are identical. The 500.65-second observer
covers both commands and reconciles zero new surviving processes, database names or
temporary entries. Historical denied resources remain recorded in [resources](../resources.md).

Retained local proof, with zero secret findings:

- [Initial fixture failure](../proof/2026-10-19-planning-authority/c4-review-read-boundary.json):
  330,732 bytes, `E60736CFC10CA92E5CFE8B3F78440DC69EF58B45FF030EB612CFBF04039856A3`.
- [Correctness pass and contaminated cost](../proof/2026-10-19-planning-authority/c4-review-read-boundary-qualified.json):
  422,865 bytes, `D235D019318A44F74BFD09371F9FDCE0CDD0C1EC0E72ACDA293BF2D437B0E6F1`.
- [Isolated baseline and component diagnostics](../proof/2026-10-19-planning-authority/c4-review-read-isolation.json):
  336,485 bytes, `F2C137670C92170137CC29CBC78A8A235FF5DE5552DAA3D2FFFD458AB1AA8D24`.

Automatic approval review previously rejected cleanup of the diagnostic trace/tool
paths with “blocked by policy.” They were not retried. This checkpoint reports clean
new fixture deltas, not complete cleanup of historical resources.
