# 02 Provider Registry Capability Manifest And Selection

## Status

- `Completed`

## Objective

- Introduce provider registry, provider profiles, capability manifests, selection policies, provider health state, and multi-provider assignment model.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R02
- R03
- R10

## Prerequisites

- SB01 completed with protocol contracts proof

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsContracts.cs`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemoryModelAccessPolicy.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderDispatchModels.cs`
- `bundle://templates/01-memory-provider-template.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Add provider profile contracts and persistence-facing models for provider instance id, display name, kind, driver kind, enabled state, health state, tenant/workspace scope, and default policy.
- Add capability manifest contracts with capability id, version, sync/async support, UI surfaces, source/feedback/event support, limits, and provider-specific extension metadata.
- Add provider selection policy contracts for default provider, explicit provider id, role/agent/workflow/process overrides, allowed/denied capability set, and fallback behavior.
- Add in-memory provider registry implementation for tests and deterministic local startup with zero providers.
- Add validation for two-provider assignment scenarios such as programming memory vs business-analysis memory.
- Align provider identity and dispatch models with the current MAF provider model files instead of creating a second provider taxonomy for memory.
- Define the zero-provider result explicitly: service registration succeeds, provider management can render, and operation attempts return typed `NoProviderConfigured` or equivalent policy results without dispatching to native Cognitive Memory, OpenAI, Qdrant, or mock providers.

## Dependency Impact

- SB06, SB16, SB17, SB18, and UI provider management depend on stable provider identity and selection.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Define provider profile and manifest models in the generic abstraction/application boundary.
2. Introduce provider registry lookup APIs for enabled providers, capability-filtered providers, provider health, and explicit provider id resolution.
3. Implement selection result models that include selected provider, reason, denied reason, capability mismatch details, and fallback decision.
4. Add tests for zero providers, one mock provider, two role-specific providers, disabled provider, unavailable capability, and fallback denial.
5. Update templates with an example HTTP provider profile and a native Cognitive Memory provider profile.
6. Add negative tests proving no implicit fallback provider is used when the registry is empty or all providers are disabled.

## Scope Exceptions

- No scope exceptions were taken.
- Browser validation is `N/A`; this subbundle changed generic contracts/application selection behavior only and did not add browser-visible UI.

## Closure Proof

- Proof manifest: `bundle://proof/SB02/manifest.md`
- Semantic invariants: `bundle://proof/SB02/semantic-invariants.md`
- Failing-first tests: `bundle://proof/SB02/transcripts/failing-first-provider-registry-tests.txt`
- Passing provider registry tests: `bundle://proof/SB02/transcripts/passing-provider-registry-tests.txt`
- Protocol manifest compatibility tests: `bundle://proof/SB02/transcripts/protocol-manifest-compatibility-tests.txt`
- Solution build: `bundle://proof/SB02/transcripts/solution-build.txt`
- Source assertions: `bundle://proof/SB02/transcripts/source-assertions.txt`
- Dependency audit: `bundle://proof/SB02/transcripts/dependency-audit-generic-registry-boundary.txt`
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
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
- The base application can start and render provider management with zero configured providers.
- Provider selection can bind different providers to different agents or workflow steps without global mutable state.
- Unsupported capability failures happen before operation dispatch and include a useful diagnostic.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB02/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB02/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB02/manifest.md` and `proof/SB02/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run provider registry tests covering zero, one, and two-provider configurations.
- Run a dependency audit proving the registry does not reference native Cognitive Memory projects.
- Run no-provider dispatch-policy tests proving operation requests stop before native Cognitive Memory, OpenAI, Qdrant, or mock driver calls.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB02 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB02 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
