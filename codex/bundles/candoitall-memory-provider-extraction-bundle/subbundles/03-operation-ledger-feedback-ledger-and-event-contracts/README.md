# 03 Operation Ledger Feedback Ledger And Event Contracts

## Status

- `Completed`

## Objective

- Define operation ledger, feedback ledger, event inbox/outbox, delayed feedback lifecycle, IPFS snapshot metadata, retention policy, and loop guard contracts.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R04
- R05
- R06

## Prerequisites

- SB01 completed

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Signals/CognitiveMemorySignalContracts.cs`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAcceptedUseSignalEmitter.cs`
- `bundle://architecture/04-runtime-operations-and-feedback.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Define generic EF-ready integration records for `MemoryOperationRecord`, `MemoryFeedbackRecord`, `MemoryContextDeliveryRecord`, `MemoryEventInboxRecord`, and `MemoryEventOutboxRecord`.
- Define retention, TTL, status, dedupe key, retry count, loop guard, and optional IPFS snapshot metadata fields.
- Define feedback stages for immediate tool result, process completion, customer acceptance, economic impact, later correction, and forget/unpin lifecycle.
- Define event types for hypothesis, verification request, source request, feedback request, maintenance signal, health signal, and provider warning.
- Add transition rules for pending, accepted, running, completed, failed, timed out, cancelled, expired, and forgotten states.

## Dependency Impact

- Async workers, proactive events, feedback UI, and final e2e feedback proof depend on this lifecycle.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Create ledger contracts and EF configuration placeholders in the generic persistence/application boundary.
2. Implement state transition validators for operation, feedback, and event records before worker implementation begins.
3. Add correlation rules linking requester, provider, operation id, context pack id, workflow/process/session ids, and source snapshot ids.
4. Add retention policy models including forget policy and optional IPFS pin/unpin lifecycle metadata.
5. Add loop guard tests for repeated provider events that would trigger memory-agent-memory recursion.

## Scope Exceptions

- No scope exceptions were taken.
- Browser validation is `N/A`; this subbundle changed generic ledger contracts/application validation behavior only and did not add browser-visible UI.

## Closure Proof

- Proof manifest: `bundle://proof/SB03/manifest.md`
- Semantic invariants: `bundle://proof/SB03/semantic-invariants.md`
- Failing-first tests: `bundle://proof/SB03/transcripts/failing-first-ledger-lifecycle-tests.txt`
- Passing ledger lifecycle tests: `bundle://proof/SB03/transcripts/passing-ledger-lifecycle-tests.txt`
- Passing full memory test suite: `bundle://proof/SB03/transcripts/passing-memory-test-suite.txt`
- Solution build: `bundle://proof/SB03/transcripts/solution-build.txt`
- Source assertions: `bundle://proof/SB03/transcripts/source-assertions.txt`
- Dependency audit: `bundle://proof/SB03/transcripts/dependency-audit-generic-ledger-boundary.txt`
- Anti-stub audit: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
- Closure decision: `Passed`

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
- A returned context pack can be matched to the requesting agent/process/session and later feedback outcome.
- Delayed economic-impact feedback can be stored without requiring the provider to cache the original context internally.
- Provider events are deduplicated and cannot recursively trigger unbounded agent/workflow loops.
- Forget/unpin policy is represented even if the concrete IPFS client is configured later.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB03/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB03/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB03/manifest.md` and `proof/SB03/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run lifecycle tests for operation states, feedback states, event dedupe, loop guard, retention expiry, and IPFS metadata unpin marker.
- Run negative tests proving feedback without a valid context delivery id is rejected or explicitly stored as unmatched.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB03 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB03 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
