# 30 Host Composition Qdrant And Cognitive Dependency Removal

## Status

- `Completed`

## Completion Summary

- Base composition no longer references native Cognitive Memory, Qdrant, or SemanticCompletion driver packages.
- Runtime host registration now loads generic Memory services and optional memory provider drivers only from explicit `Memory:Providers:*` configuration.
- Native Cognitive Memory module assembly discovery was removed from the base host.
- Legacy host `/api/cognitive-memory` endpoint partials were retired into a provider-neutral compatibility surface with a contract endpoint and 410 Gone guidance.
- Zero-provider runtime, optional-driver registration, native remote driver, web build, solution build, source-boundary audits, and anti-stub/XML-doc audits pass.
- SB31 may proceed; host data migration/export/retirement and legacy test rebalance remain downstream.

## Objective

- Remove native Cognitive Memory and Qdrant from base CanDoItAll composition, module assembly discovery, startup, endpoints, and MAF registrations; enable optional provider configuration only.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R17
- R11

## Prerequisites

- SB29 gate passed

## Exact Source References

- `repo://src/App/CanDoItAll.Composition/CanDoItAll.Composition.csproj`
- `repo://src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `repo://src/App/CanDoItAll.Composition/ModuleAssemblies.cs`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/CanDoItAll.Modules.CognitiveMemory.csproj`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Remove direct project references from base composition to `CanDoItAll.Modules.CognitiveMemory`, native Cognitive Memory implementation projects, Qdrant/RAG drivers, and native module assembly markers.
- Replace direct native API endpoints and DI registrations with generic memory module endpoints and optional provider/driver registrations.
- Ensure base startup works with only PostgreSQL plus the app and zero memory providers configured.
- Add optional configuration path for native provider remote driver and optional Qdrant only inside native provider/projection configuration.
- Add architecture guards for forbidden references in Composition, Web, MAF, generic memory, and base startup paths.
- Remove current base registrations for `AddConfiguredQdrantRagDriver(...)` and `AddCognitiveMemoryModule()` unless they are behind explicit optional provider configuration.
- Remove current base composition references to SemanticCompletion/RAG driver packages when those are only needed by native or other explicitly configured providers.

## Dependency Impact

- Startup and final closure depend on removing direct base dependencies.

## Validation Depth

- `Critical decoupling`

## Implementation Steps

1. Edit composition project references and runtime service registration so native memory and Qdrant are no longer base dependencies.
2. Update `RuntimeHostServiceCollectionExtensions` and `ModuleAssemblies` to load generic memory module and optional provider drivers only.
3. Retire or redirect current `CognitiveMemoryApi*` endpoints to generic memory endpoints or native service endpoints as appropriate.
4. Add startup tests for zero providers, mock provider only, HTTP provider, and native provider configured.
5. Add forbidden-reference tests for composition, web, and MAF projects.
6. Add an assertion that zero-provider startup does not instantiate native Cognitive Memory services, Qdrant clients, OpenAI semantic completion clients, or mock providers.

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
- Base CanDoItAll starts without native Cognitive Memory module, native service, Qdrant, or semantic/RAG driver configuration.
- Native Cognitive Memory can still be enabled as an optional provider through generic configuration.
- No base composition or MAF project has a direct reference to native Cognitive Memory implementation assemblies.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB30/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB30/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB30/manifest.md` and `proof/SB30/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run startup tests with Qdrant/native memory configuration absent.
- Run forbidden-reference audit across `src/App`, `src/MAF`, generic memory projects, and solution/project files.
- Run composition audits specifically covering `CanDoItAll.Composition.csproj`, `RuntimeHostServiceCollectionExtensions.cs`, and `ModuleAssemblies.cs`.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB30 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB30 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
