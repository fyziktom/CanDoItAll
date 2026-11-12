# Manager authority at aggregate publication

Status: qualified. All 56 discovered cases passed.

The prerequisite is signed commit `abacea80b`, with 331 passing waiting-result,
snapshot and publication tests. The reviewed design is
[current manager actions](../plan/P07-current-manager-actions.md).

The guarded execution-run mutation overload checks current authority after acquiring
the selected publication keys and before reacquiring the workspace lock. The mutation
delegate is evaluated once; its original source witness is rechecked before committing.
Guarded no-ops also require authority and do not advance revisions. Unsupported stores
refuse explicitly. Existing unguarded writers keep their existing path.

The authority callback performs bounded read-only observation. It must not mutate
workspace or Process state. Later Project publication still requires fresh authority
inside its existing SQL transaction; this file guard alone does not authorize effects.
No project reference, persistence schema or partial class is added.

The candidate is `.artifacts/concurrent-mutations/validation/c4-manager-run-authority`.
Ownership was recorded before creation. It reuses standard Release outputs and the
existing isolated PostgreSQL fixture for observation, without provisioning a host.
The affected Core and AgentFramework Persistence projects are built directly, then
Integration is refreshed. Discovery must confirm 56 cases: eight new authority cases
and 48 existing snapshot/publication/recovery cases. The new cases are the positive
gate before the existing regressions.

Required proof includes revocation during a selected-key wait; unrelated catalog
publication while authority observation is paused; allowed and denied no-ops;
cancellation and authority failure releasing keys; stale source bytes after observation;
and unsupported owners refusing before entering legacy writes. Source and runtime
inputs are frozen for execution. Final resource observation, portability enforcement,
architecture comparison and documentation validation remain required.

This checkpoint does not implement durable manager choice, public API, Process
executor activation, parent reconciliation, Workflow/settings/SPA or sustained capacity.
Earlier policy-denied cleanup resources remain recorded and excluded from deletion.

## Qualification

Core, AgentFramework Persistence and Integration Release builds passed. Discovery
matched all 56 expected cases and source methods; all five execution lanes passed.
The eight new authority cases passed before the 48 existing regressions. The observer
covered all five commands; final reconciliation found zero surviving observed processes,
new database names or retained temporary entries. Historical cleanup denials remain.

The 9,856 source inputs and 1,807 runtime inputs remained unchanged:

- Source: `C288FD2191C53637B2AB4281DF1C66BB7CAFDB1A4742848507325D55507E5E5F`.
- Runtime: `3D5178B9516A5A5E61736D7C358F7E502890DACFC6BCE7BDDAEA411F061EDFD8`.
- Locally retained proof: `proof/2026-10-19-planning-authority/c4-manager-run-authority.json`,
  112,113 bytes, SHA-256 `3A3EA69B81B37C0B90EFA98FC5375B811BE407A0DD922C004DB45526D979B886`.
  Secret scan: zero findings.

CodeAnalytics before `snap-20261021130755-803d411d`, after
`snap-20261021133932-803d411d`: 920 types remain; members increase from 7,275 to 7,278
for the guarded capability and shared file mutation preparation. Project references,
dependency edges, four existing scoped cycles and 277 findings are unchanged.
The 189 existing collector diagnostics remain; direct builds passed. No whole-solution
clean result is claimed.

Portability tooling tests, secret-scanner tests, rendering tests/register, documentation
and whitespace checks passed. Final portability enforcement passed with all 15,663
reviewed findings unchanged; no baseline refresh was needed.
