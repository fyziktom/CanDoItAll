# 01 Protocol Envelope And Schema Contracts

## Status

- `Completed`

## Objective

- Define Memory Protocol v1 envelopes, request/response records, capability identifiers, versioning, policy, budget, provenance, extension payload, and serialization validation.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R01
- R03
- R05
- R06
- R07

## Prerequisites

- none

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Common/CognitiveMemoryProviderContracts.cs`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Common/CognitiveMemoryCommonContracts.cs`
- `bundle://architecture/02-protocol-contract-model.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Create or update generic Memory Protocol v1 DTOs/records in the planned abstraction boundary, not inside native Cognitive Memory implementation code.
- Define envelope records for query, ingestion, feedback, source request, provider event, operation status, operation result, and health/capability exchange.
- Define stable value objects/enums for provider id, operation id, context pack id, correlation id, causation id, operation kind, protocol version, capability id, sensitivity, retention, and budget.
- Define structured context blocks for workspace, project, process/workflow, agent/session/requester, policy, source provenance, and extension payloads.
- Add serialization tests and compatibility fixtures proving simple text-only providers can be represented without losing structured metadata.
- Document reserved extension namespaces such as `host.candoitall.*`, `native.cognitiveMemory.*`, and `provider.vendor.*`.

## Dependency Impact

- SB02-SB34 depend on stable protocol names, envelope fields, versioning, and structured context shape.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Create the generic protocol boundary first and keep existing native contracts as migration reference only.
2. Map current `CognitiveMemoryProviderContracts` and `CognitiveMemoryCommonContracts` concepts to generic names without copying native semantics into generic contracts.
3. Add immutable DTO/record types and validation helpers for required ids, protocol version, operation kind, capability id, and extension namespace.
4. Add JSON serialization round-trip fixtures for sync query, async accepted operation, provider event, feedback request, and source request.
5. Add negative tests for missing correlation ids, unsupported protocol version, invalid capability id, and native-only extension branching in generic code.
6. Update protocol documentation and execution report proof.

## Scope Exceptions

- No known scope exceptions for this subbundle at preparation time.
- If implementation discovers an exception, document it in `reviews/01-execution-report.md` and stop before downstream work if the exception affects a phase gate.

## Do Not Do

- Do not implement downstream subbundles early.
- Do not introduce direct generic-memory or MAF references to native Cognitive Memory implementation types.
- Do not add Qdrant as a base runtime dependency.
- Do not expose host EF entities or DbContext instances to memory providers.
- Do not duplicate memory operation dispatch logic outside the shared handler.

## Acceptance Checklist

- The implemented surface is observable through focused tests or explicit proof artifacts.
- Dependency boundaries from `requirements/03-non-negotiable-boundaries.md` remain intact.
- No downstream subbundle work is silently implemented or assumed.
- Execution report is updated with proof paths, command transcripts, and gate result.
- Generic protocol records contain no `CognitiveMemory*` type names except in migration tests or documented namespace examples.
- Structured context supports project, process step, workflow node, agent identity, requester reason, policy, source provenance, sensitivity, budget, and extension facts.
- A simple query-response provider can ignore advanced fields while the host still records them for feedback and traceability.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB01/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB01/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB01/manifest.md` and `proof/SB01/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run contract serialization tests for every envelope listed in `architecture/02-protocol-contract-model.md`.
- Run a source audit proving generic protocol files do not reference Qdrant or native Cognitive Memory implementation namespaces.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Passed. Downstream subbundles may start because SB01 proof is recorded in `bundle://proof/SB01/manifest.md`, the acceptance checklist passed, and no phase-gate blocker remains.

## Execution Proof

- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`.
- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-memory-protocol-tests.txt`.
- Passing transcript: `bundle://proof/SB01/transcripts/passing-memory-protocol-tests.txt`.
- Solution build transcript: `bundle://proof/SB01/transcripts/solution-build.txt`.
- Source assertions: `bundle://proof/SB01/transcripts/source-assertions.txt`.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`.
- Browser validation: N/A; no browser-visible surface changed.

## Suggested Agent Prompt

```text
Implement subbundle SB01 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
