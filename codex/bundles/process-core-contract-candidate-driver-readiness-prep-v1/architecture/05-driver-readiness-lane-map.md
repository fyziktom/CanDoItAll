# Driver Readiness Lane Map

## Scope
This is documentation-only readiness work. It names future helper-driver lanes and the evidence each lane would need before any production API, DI registration, registry, runtime dispatch, or tool exposure is created.

## Lane Summary

| Lane | Purpose | Candidate inputs | Candidate outputs | Current status |
| --- | --- | --- | --- | --- |
| Route decision helpers | Explain why a process route is eligible, blocked, or deferred. | Route-stage descriptors, route kind, run status, step status, expected artifact facts, and pure eligibility decisions. | Read-only decision explanations and verification evidence. | Candidate later for pure rules only; no driver API now. |
| Evidence and projection helpers | Verify artifact expectation, projection, lineage, and satisfaction outcomes. | Expectation snapshots, artifact metadata, projection observations, provider-native browser evidence names, and lineage keys. | Read-only mismatch reports, confidence notes, and suggested investigation paths. | Candidate later after projection/validation parity remains green. |
| Runtime verification helpers | Check that execution, retry, provider repair, finalizer, and smoke-proof evidence is internally consistent. | Execution run snapshots, retry/fallback journals, finalizer inputs, proof transcripts, and source assertion results. | Verification reports; no state transition, lease, claim, or journal writes. | Verification-only candidate later; no runtime hook now. |
| Domain-specific software-development helpers | Interpret process evidence for .NET, Rust, Office, and business-analysis work without replacing the process module. | Domain-scoped files, command outputs, artifact contracts, and process step evidence. | Domain-specific validation findings and suggested next proof. | Documentation-only; future permission model must decide whether execution is allowed. |

## Lane Boundaries
- No lane may own EF-backed candidate hydration, claim leases, heartbeat renewal, transition execution, workspace writes, storage writes, AgentFramework execution, technical-agent binding, provider repair, finalizer application, or process state mutation.
- No lane may introduce a production interface, registry, service registration, route handler, manager tool, or runtime dispatcher in this bundle.
- Any future lane must start with a narrow read model and failing negative tests that prove it cannot mutate process state unless an explicit execution-capable mode is approved in a later bundle.

## Evidence Links
- Route and pure-rule evidence: `bundle://proof/SB027/manifest.md`
- Projection and validation evidence: `bundle://proof/SB024/manifest.md`
- Execution and finalizer evidence: `bundle://proof/SB021/manifest.md`
- Driver safety model: `bundle://architecture/06-driver-safety-permission-model.md`

## Current Decision
The next work may prepare a narrow Process Core discussion for pure rule/read-model descriptors. It must not add production helper-driver APIs until Gate J and Gate K prove the documentation-only boundary remains intact.
