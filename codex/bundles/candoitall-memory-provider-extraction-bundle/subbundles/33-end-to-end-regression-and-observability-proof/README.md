# 33 End To End Regression And Observability Proof

## Status

- `Completed`

## Objective

- Run full e2e and observability proof for base startup without native/Qdrant, two mock providers, MAF tool/executor, source ingestion, native remote provider, events, feedback, and UI.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R16
- R17
- R19

## Prerequisites

- SB32 completed

## Exact Source References

- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/CognitiveMemoryReviewUiPlaywrightTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/DatabaseRuntimeSwitchingIntegrationTests.cs`
- `bundle://reviews/01-execution-report.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Run end-to-end regression proof for base startup without native/Qdrant, zero-provider UI, two mock providers, MAF tool/provider selection, workflow executor, Source Gateway ingestion, async operation, provider event, feedback lifecycle, native remote provider, and UI projection.
- Capture observability proof for operation ledger, feedback ledger, event inbox/outbox, source requests, provider health, worker processing, and error states.
- Capture browser proof for generic UI, query/chat, operations/status, feedback, provider-specific UI fallback, RCL, and iframe surface when available.
- Run final source/dependency audits and startup profiles before final cleanup.
- Block closure if any critical path falls back to old native direct calls or manually seeded test-only behavior.
- Prove zero-provider behavior end to end: base startup, provider management UI, MAF tool/executor/context paths, and operation APIs must stop with typed no-provider/disabled results without dispatch.
- Prove current MAF registration paths and the unified source snapshot contract family are exercised in production paths.

## Dependency Impact

- Final cleanup depends on comprehensive proof across startup, runtime, UI, source, feedback, and native provider paths.

## Validation Depth

- `Closure-critical regression`

## Implementation Steps

1. Run base application startup with PostgreSQL and no Qdrant/native memory provider configured.
2. Run configured mock-provider scenario with two providers assigned to different agents/workflows.
3. Run MAF tool and workflow executor scenarios and verify shared operation handler records.
4. Run manual ingestion/source request and delayed feedback scenarios and inspect ledger records.
5. Run native remote provider scenario if native service is available and capture protocol/API evidence.
6. Run browser validation for provider management, query/chat, async operation, feedback, and provider-specific UI fallback/surfaces.
7. Collect logs, metrics, screenshots, transcripts, and source-audit outputs in `proof/SB33`.
8. Run final audits for no hidden native/Qdrant/OpenAI/mock fallback and no incompatible source snapshot contract family.

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
- Full e2e proof covers base app without Qdrant/native memory and optional native provider enabled separately.
- Two providers can run in parallel and be selected differently by agent/workflow context.
- Feedback can be attached after context delivery and later process/outcome event.
- Observability artifacts make operation/event/feedback/source lifecycles inspectable.
- Zero-provider proof covers UI, APIs, tools, workflow executors, and context contributors.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB33/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB33/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB33/manifest.md` and `proof/SB33/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run the relevant component or Playwright tests and capture large-screen plus narrow-width screenshots where layout or provider switching is visible.
- Run native-service build/test commands from the `CanDoItAll.CognitiveMemory` repository after confirming real target paths, and capture transcript paths in the manifest.
- Run final e2e scripts/tests and capture command transcripts, screenshots, logs, and ledger snapshots.
- Run final dependency/source audit proving no direct native/Qdrant base dependency remains.

## Browser Validation Logging

- Record route, viewport, Playwright actions, assertions, screenshot paths, and screenshot review questions in `reviews/01-execution-report.md`.

## Progression Gate

- Downstream subbundles may start. SB33 proof is recorded in `bundle://proof/SB33/manifest.md` and `bundle://proof/SB33/semantic-invariants.md`; focused runtime, generic memory, MAF, component, Playwright, integration, native build/test, main build, source audit, anti-stub audit, ledger snapshot, and screenshot proof passed.

## Completion Notes

- End-to-end regression proof passed through production runtime services, workers, ledgers, Source Gateway, provider health/error handling, and MAF memory paths.
- Browser proof passed for `/memory` at `1440x1000` and `390x900`, covering zero provider, two explicit mock providers, query/context pack, feedback, manual ingestion, operations, RCL/iframe, and fallback surfaces.
- Native `CanDoItAll.CognitiveMemory` build/test proof passed separately, preserving the base host native/Qdrant decoupling boundary.

## Suggested Agent Prompt

```text
Implement subbundle SB33 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
