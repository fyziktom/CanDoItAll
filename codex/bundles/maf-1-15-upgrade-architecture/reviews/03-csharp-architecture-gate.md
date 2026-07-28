## C# Architecture Gate Result

Status: Pass

### Findings

| Severity | Finding | Evidence | Required action |
|---|---|---|---|
| Information | Direct non-streaming handoff depth observation occurs after the inner workflow completes; it preserves the native 1.15 response but is not a pre-mutation guard. | `MafHandoffWorkflowFactory.cs`; direct/streaming characterization tests; `architecture/05-csharp-governor-review.md` | Keep production execution on the incremental streaming path. Treat any transition-boundary redesign as separately reviewed work. |
| Information | The inherited `System.Security.Cryptography.Xml` 10.0.7 advisories remain visible. | Restore/build `NU1903` baseline | Resolve in the owning dependency-security work; do not suppress it as part of the MAF migration. |

### Dependency direction

Pass. MAF SDK types remain inside the MAF adapter, workflow adapter, hosting, and
test projects. Core and Models remain provider-neutral. No new project reference
or dependency cycle was introduced. CodeAnalytics snapshots
`snap-20260728042508-0d7f96ce` and `snap-20260728054006-aa62cd27` report no cycle
in the reviewed boundary.

### Partial-class policy

Pass. No partial class was introduced or expanded. The new options factory has
one responsibility: applying the shared MAF 1.15 compatibility policy.

### Testability proof

The common options policy, stable approval identifiers, native session
serialize/scrub/restore binding, timeout/cancellation behavior, terminal response
projection, loaded assembly identities, resolved package graph, and direct plus
streaming handoff behavior are covered through deterministic seams. Focused
results at the gate were 38/38 unit tests and 6/6 handoff integration tests.
Negative tests cover missing identifiers, persistence failure, duplicate or
unknown approval responses, and cancellation.

### Closure decision

Architecture work may proceed to runtime and UI closure. Reopen this gate if a
future change adds per-request migration persistence, inspects private MAF JSON,
introduces a replay bridge, moves SDK types into Core/Models/UI, adds a project
reference, or changes the workflow transition boundary.
