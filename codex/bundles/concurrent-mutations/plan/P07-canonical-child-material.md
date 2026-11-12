# Canonical Process child/material resolution

Status: implemented with [169 passing selected cases](../validation/2026-10-19-canonical-child-material.md)
across an exact test-only repair bridge, following signed shared-loop checkpoint `9f2bdbcaa`.
This slice connects the existing Process storage owners to product consumers. It does
not enable dispatch or publication by itself.

## Responsibility and dependencies

Expose `EfProcessDecisionChildQuery` through `IProcessDecisionChildReader` in Process
Application. The query and its bounded indexed SQL stay in Persistence; the composition
root registers the same scoped implementation through the port. Workbench already
references Application and must not acquire a dependency on Process Persistence or the
Processes product module. AgentFramework Core remains independent of Processes.

A neutral binding records the exact child admission, original material scope/stamp and
execution contract. A cohesive Application resolver compares that binding with the
canonical child, immutable assignment admission and captured material. Its result exposes
the original assignment, child observation and payload to the owning integration adapter.
It never interprets Agent journals, Workbench content, provider configuration or actor rights.

The binding is a bounded, versioned canonical wire value, not authority. Reject unknown
versions, missing/null fields, unsupported values, duplicate/noncanonical JSON, substituted
scope/material/grant/contract and missing canonical records. Compare both the complete
Process stamp and the content contract; the Agent adapter separately verifies the exact
payload digest and original capture time. Consumer-selected supported contracts must
come from registration, not merely repeat the value in an incoming binding.

## Original data, authority and races

Historical children and material remain readable after closure, parent reconciliation or
refresh if current actor/source read authorization allows it. The resolver therefore does
not require the current generation, an open allocation or a live dispatch claim. It returns
that state explicitly. Source authorization and provider admission separately require their
current permissions, original child and live grant/claim. Publication independently uses
the existing enlisted root/generation fence. A successful read is never a dispatch token.

RunSnapshot with a shared scope must bind the original root-owned scope. StepSnapshot and
legacy local material must bind their original local owner and step. Actual executor owner,
namespace, policy, content protocol and supported execution contract come from the saved
assignment. No new material, allowance, assignment or child is created by reading it.

Separate bounded reads are acceptable here because material and admitted policy are
immutable. The observation of mutable child state is advisory and cannot replace the
transaction fence. Do not hold a database transaction across filesystem or provider calls.

## Cost and validation

Resolve one child, one assignment and one material by exact keys. Never enumerate run
history or siblings. Load the complete payload during original admission/recovery and
initial basis loading, not each provider iteration. The existing small child/source reads
remain the current authority checks. Reuse original immutable material within a child;
do not add a second cache or index.

Prove canonical and malformed binding behavior, actual SQL resolution and composition,
changed scope/grant/material/contract rejection, shared-root descendant inheritance,
historical read after closure/reconciliation, and reopening through a fresh owner. The
existing publication race and bounded-query tests remain the fence/performance regressions.
Run affected standard Release builds, exact discovered tests, scoped dependency review,
the complete portability-static procedure and observed resource reconciliation. Reuse the
isolated PostgreSQL fixture. Do not activate Process/Workflow settings in this slice.
