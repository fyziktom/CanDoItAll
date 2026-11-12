# Review intent compatibility and read-cost checkpoint

Status: compatibility passed; complete HTTP read capacity failed and remains open.

Prerequisite: signed `34c0fce42`; all 76 Process review HTTP cases passed. The
[prepared design](../plan/P07-review-compatibility-and-read-cost.md) specifies the
v1 compatibility contract, explicit finite-host reference and remaining boundaries.

The only production edit extracts the existing Workbench digest expression into
an internal method on its current owner. Field names, order, domain and serializer
options remain identical. Pure golden fixtures cover the actual Core, Workbench
and manager calculations. The nine reference values were established from the
unchanged signed v1 expression. The diagnostic run deliberately compared against
placeholders and reported nine mismatches; it is retained separately and is not
passing proof. Final tests use fixed literals and must pass independently.

The expression comparison verified the original `Domain`, `Process`,
`OriginalParent`, `Actor` property order, default serializer and production call
mapping against `34c0fce42`. CodeAnalytics compares
`snap-20261021171238-58935274` with `snap-20261021174555-58935274`: one internal
member added, no type or dependency-edge changes, no finding changes, and six
existing intra-project cycles unchanged. Both snapshots retain 189 pre-existing
workspace diagnostics; affected direct Release builds passed.

The HTTP measurement uses real accepted plans and the registered reader, current
access checks, serializer and SQL owners. Small prompts are an explicit fixture
argument; production byte and allocation bounds remain unchanged. Existing fixture
callers keep their prior prompt size. Setup and warm request cost are separate.

Build-backed discovery confirmed 22 cases. All nine fixed compatibility cases and
seven actual HTTP recovery regressions passed. All six scaling cases failed the
one-second reference at their first measured request after two warmups:

| Parent mode | Steps | HTTP ms | Process allocation estimate, bytes | Process SQL commands | Response bytes |
| --- | ---: | ---: | ---: | ---: | ---: |
| RunSnapshot | 1 | 1,532.4 | 79,570,368 | 27 | 1,827 |
| RunSnapshot | 128 | 1,242.6 | 79,888,192 | 27 | 1,827 |
| RunSnapshot | 1,024 | 1,395.1 | 82,152,664 | 27 | 1,827 |
| StepSnapshot | 1 | 1,370.1 | 79,723,120 | 27 | 1,827 |
| StepSnapshot | 128 | 1,446.6 | 80,305,304 | 27 | 1,826 |
| StepSnapshot | 1,024 | 1,282.5 | 83,728,912 | 27 | 1,826 |

These are six individual observations, not medians or sustained percentiles.
Allocation includes process activity; query counts cover Process contexts only.
The failure prevented the remaining four measurements and final no-write/provider
assertions in each scale case. Setup is separately recorded. The actual HTTP
recovery regressions still passed their complete effect/accounting assertions.

The fixed cost dominates even a one-step plan. Removing sibling runtime reads alone
cannot be assumed to solve it. Trace existing Agent telemetry, workspace construction,
file reads/serialization and SQL before selecting the repair. Keep authentication,
original-source disclosure, current manager checks and publication fences intact.
Do not raise the reference or treat the small response as proof of a cheap read.

Workbench, Web, Unit and Integration Release builds passed. The full portability
scan/enforcement, rendering register, documentation and whitespace checks passed
without rewriting the baseline. Source and runtime remained unchanged throughout
the observed test run:

- Source: `4F7206E9D73891F7C081A213FC077211CE26FA372C632DF11D8E8C58AE8D3E16`
  across 9,884 inputs.
- Runtime: `E52976C14E5FDE57A81C1D87DFA7218B6A78671FF449E119BFF82B934E975FFB`
  across 6,655 copies.
- All five commands were covered by the 647.976-second observer. Reconciliation
  found zero new surviving processes, database names or temporary entries.
- Failed proof: `proof/2026-10-19-planning-authority/c4-review-compatibility-read-cost.json`,
  97,195 bytes, SHA-256
  `5DC8A7FD16EF7D8FC73C6A1B5D92303C550B3D8888B7AC424019F1AA37595B8B`,
  zero secret findings; retained locally under the existing ignored proof policy.

Architecture inspection passes for the pure extraction. Capacity does not pass,
so parent/executor activation cannot borrow qualification from this checkpoint.
Subsequent documentation changes only record these outcomes. Earlier cleanup
denials remain baseline-only; SPA remains on its own branch.
