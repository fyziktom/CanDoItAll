# 24 Native Repo Solution And Service Scaffold

## Status

- `Completed`

## Objective

- Scaffold target native repository/projects, service host, contracts, API skeleton, worker host, UI package, MAF package, and README/dependency rules.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R14
- R15

## Prerequisites

- SB23 gate passed
- Implementation agent has inspected `C:\repositories\CanDoItAll.CognitiveMemory`, which exists and was observed during bundle refresh as an unscaffolded repository containing only `README.md`

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/CanDoItAll.Modules.CognitiveMemory.csproj`
- `bundle://architecture/06-native-service-extraction.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://templates/02-subproject-template.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Inspect the real `CanDoItAll.CognitiveMemory` repository before editing and align target project names with existing repo conventions.
- Scaffold native service solution/projects for Contracts, Domain, Persistence, Application, Projection.Rag, Maf, Service, Workers, and UI as appropriate.
- Add dependency direction rules so native service may depend on MAF abstractions where needed but not on the main CanDoItAll Agent module or main app module.
- Add minimal service host, options, configuration samples, health endpoint, and build/test skeletons.
- Add placeholder protocol mapping tests that compile against generic Memory Protocol contracts.
- Treat the target repo as a real local repository, not a missing ZIP artifact; create the solution/projects there only during SB24 implementation.
- Keep any in-process native bridge as an explicitly configured provider profile for migration, never as a base startup fallback.

## Dependency Impact

- All native extraction work depends on correct project boundaries and solution wiring.

## Validation Depth

- `Native service foundation`

## Implementation Steps

1. Open the target repository and document actual existing structure in the SB24 proof manifest.
2. Create or align solution/project structure according to `architecture/01-target-solution.md` without moving engine code yet.
3. Wire common packages, nullable/analyzer settings, DI conventions, and test projects consistently with CanDoItAll style.
4. Add health endpoint and startup configuration that does not require Qdrant by default.
5. Add dependency guard tests for native repo project direction and forbidden main module dependencies.
6. Record the initial native repo state (`README.md` only as of the 2026-07-05 bundle refresh) and any divergence discovered at SB24 start.

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
- The native repo has a compilable service skeleton and project layout ready for DB/domain migration.
- Native projects do not reference the main CanDoItAll module or Agent module implementation projects.
- Qdrant is represented only as an optional projection package/configuration path.
- The native service skeleton can start its health path without Qdrant and without the main CanDoItAll host.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB24/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB24/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB24/manifest.md` and `proof/SB24/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run native-service build/test commands from the `CanDoItAll.CognitiveMemory` repository after confirming real target paths, and capture transcript paths in the manifest.
- Capture the actual target repo path/branch/commit or local status in `proof/SB24/manifest.md`.
- Run native repo build/tests for scaffold projects and dependency guard tests.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB24 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB24 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
