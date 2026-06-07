# Next Pure Family Decision

## Decision
- Decision: declare Core stable enough for driver-contract proposal work.
- No additional pure family is required before the next roadmap phase.
- No broad dispatcher, runtime, storage, workspace, AgentFramework, finalizer, transition, claim, or retry extraction is approved.

## Reasoning
- Execution, finalizer, diagnostics, projection, validation, routing, subprocess, and artifact rule families now have bounded Core descriptors or pure rules.
- Adapter ownership and public API stability are guarded by tests and proof transcripts.
- Remaining behavior is side-effectful and should stay in the process module until a separate bundle proves a smaller deterministic extraction.

## Future Candidate Filter
A future pure family may be proposed only if it:
- Accepts immutable snapshots or primitive value facts.
- Has deterministic output.
- Has no storage, filesystem, workspace, AgentFramework, provider, finalizer, retry, claim, transition, or external-service dependency.
- Has an adapter-owned module boundary.
- Has focused tests and an updated public API owner classification.
