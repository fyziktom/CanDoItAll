# 27 Native Protocol Api And Remote Provider Driver

## Status

- `Completed`

## Completion Summary

- Native service Memory Protocol API endpoints were implemented for health, manifest, query, ingestion, operation status, feedback, event polling, source request, and provider-specific native probe.
- The main generic memory runtime gained an opt-in `NativeRemote` driver that translates native profile extension settings into the existing generic HTTP driver without compile-time native implementation references.
- Native API contract tests, main native remote driver tests, native/main solution builds, boundary audits, and anti-stub audits are captured under `bundle://proof/SB27/`.

## Objective

- Expose native Cognitive Memory through Memory Protocol v1 API and implement the main-app remote/native provider driver.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R16
- R03
- R17

## Prerequisites

- SB26 completed

## Exact Source References

- `C:\repositories\CanDoItAll.CognitiveMemory\src\CanDoItAll.CognitiveMemory.Service\CognitiveMemoryProtocolApi.cs`
- `repo://src/Memory/CanDoItAll.Memory.Http/NativeRemoteMemoryProviderDriver.cs`
- `bundle://architecture/02-protocol-contract-model.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Implement Memory Protocol v1 HTTP/API endpoints in the native service for query, ingestion, feedback, operation status, event polling/push, source request, health, and capability manifest.
- Implement native protocol mappers from generic envelopes to native application commands and from native outputs to generic context packs/events/results.
- Add optional advanced native endpoints only under provider-specific namespace/surface and never as generic runtime requirements.
- Implement main-repo native remote provider driver profile that talks to the native service through generic driver abstractions.
- Add contract tests between main generic driver and native service test host.

## Dependency Impact

- Main host decoupling depends on native provider availability through generic protocol.

## Validation Depth

- `Critical native integration`

## Implementation Steps

1. Define native API routes and DTO mapping layer using Memory Protocol contracts.
2. Implement sync query path, async accepted path, ingestion path, feedback path, status path, event path, and health/manifest path.
3. Implement main-repo native remote driver only after the native service API contract is stable.
4. Add integration tests using a native service test host and main generic provider driver.
5. Document endpoint versioning and provider-specific advanced surface boundary.

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
- The main app can communicate with native Cognitive Memory as a provider through the same protocol used by other memory providers.
- Native service advertises capabilities and unsupported advanced operations fail predictably.
- Advanced native APIs do not become required dependencies of generic memory runtime.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB27/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB27/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB27/manifest.md` and `proof/SB27/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run native-service build/test commands from the `CanDoItAll.CognitiveMemory` repository after confirming real target paths, and capture transcript paths in the manifest.
- Run native service API contract tests and main-repo native remote driver integration tests.
- Run negative tests for unsupported capability, invalid protocol version, timeout, and malformed request.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB27 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB27 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
