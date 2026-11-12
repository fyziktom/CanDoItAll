# Process decision execution contract admission

This refines [the runtime design](P07-process-decision-runtime.md) after the
[child publication foundation](P07-process-child-publication.md). It is an immutable
wire and assignment contract, not activation of a governed executor.

Implemented with [114 selected passing behaviours](../validation/2026-10-19-process-execution-contract.md)
across an exact test-only repair bridge. Actual governed execution remains gated.

## Decision before implementation

The current policy codec deliberately accepts only schema 1. Its capability flags
describe policy resolution, but do not identify the business participant or an executor
protocol. A policy alone must never select a new executor. Adding an optional field to
schema 1 would weaken its existing strict reader and could let an older writer omit the
new obligation.

Retain schema 1 byte-for-byte when no execution contract was admitted. Schema 2 requires
an exact immutable contract identity, positive contract version and Agent/Workflow
executor owner. Schema 1 rejects even a null execution-contract field; schema 2 rejects
an absent/null contract. Future envelope versions, duplicate/unknown fields and invalid
identities fail before work. Contract IDs are opaque GUIDs owned by actual adapters;
Process Runtime must not enumerate Workbench business features or reference Agent SDKs.

The Runtime value and exact-match check own these invariants. The existing codec owns
wire version selection. Prepared-launch fingerprints and assignment repair protection
already cover the encoded policy; they must include the new contract without another
index, column, serializer or migration. The registered adapter must later supply its
actual supported identity/version/owner to the match check. A caller-supplied declaration
is never itself evidence of support. Keep both executor and persisted source-authority
refusals closed until inherited material, child journals and the real effect path exist.

No new interface, project reference, partial runtime, service-location or generic plugin
registry is justified. Existing Module composition will choose the concrete adapter.
This is bounded constant-size validation, with no additional database round trip or lock.
CodeAnalytics baseline `snap-20261019193102-d1ce2c09` contains four Process projects and
no detected cycles, with 190 existing load diagnostics and the recorded DI collector
ambiguity. Direct builds and composition tests supplement that incomplete static view.

## Acceptance and validation

- Preserve schema 1 encoding and the existing policy-free prepared-launch golden bytes.
- Round-trip both executor owners through strict schema 2; reject schema downgrade,
  missing/null/duplicate/unknown fields, invalid IDs/versions/owners and any mismatch.
- Persist schema 2 through preparation, runtime commit and reload; deny adding,
  removing, changing owner, changing version or replacing the contract during repair.
- Prove schema 2 remains refused by the current Agent/Workflow executors before launch;
  retain legacy execution and original persisted source-authority regression lanes.
- Build Runtime, Process host composition and the owning Unit/Integration assemblies;
  confirm exact discovery, run selected PostgreSQL tests on the existing isolated
  fixture, enforce portability without baseline changes and verify resource deltas.

The next adapter must preserve the parent's original material and capture time.
Child startup cannot renew basis age. A shared RunSnapshot refresh must fence every
affected child at the parent scope; a step-local generation cannot stand in for that
scope. These remain separate runtime obligations, not claims made by this envelope.
