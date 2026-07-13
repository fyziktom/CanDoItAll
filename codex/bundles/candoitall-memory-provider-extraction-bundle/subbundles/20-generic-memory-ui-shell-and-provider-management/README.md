# 20 Generic Memory Ui Shell And Provider Management

## Status

- `Completed`

## Objective

- Add generic Memory UI module shell, navigation, provider list, provider profile editor, capability/health display, and zero-provider state.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R12
- R02

## Prerequisites

- SB19 gate passed

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Pages/CognitiveMemoryPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemorySettingsTab.razor`
- `bundle://architecture/05-ui-composition.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Create generic Memory UI shell/module wrapper with provider list, provider detail, capability manifest display, health/status, default selection, and empty-state behavior.
- Expose common tabs for providers, operations, events, feedback, and common query/chat entry without assuming native Cognitive Memory exists.
- Wire UI to generic memory application services and mock providers for deterministic demos/tests.
- Add safe error states for unavailable provider, missing capability, timeout, and zero providers.
- Add component/browser tests for navigation, zero-provider startup, two-provider selection, and provider health rendering.
- In zero-provider mode, provider management must remain usable while provider-backed actions are disabled or return typed no-provider diagnostics; do not auto-create or auto-select a mock/native provider.

## Dependency Impact

- Query UI, operation UI, and provider-specific surfaces depend on shell/provider management.

## Validation Depth

- `UI foundation`

## Implementation Steps

1. Split current native `CognitiveMemoryPage.razor` concepts into generic provider shell vs native-specific advanced tabs.
2. Implement generic provider management UI using generic provider profiles/manifests only.
3. Add routes/navigation for the new generic memory module and ensure the route does not instantiate native services by default.
4. Wire two mock provider profiles for test/demo proof.
5. Capture large-screen and narrow-width screenshots for provider management and empty-state behavior.
6. Capture proof that zero-provider UI renders without native Cognitive Memory, Qdrant, OpenAI, or mock-provider registration.

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
- The memory UI loads with zero providers and with mock providers without requiring native Cognitive Memory or Qdrant.
- Zero-provider UI does not offer executable query/ingest/feedback actions that would dispatch without an explicit provider.
- Provider management shows capabilities and health in a provider-agnostic way.
- UI navigation and DI registration do not instantiate native memory services unless a native provider profile is configured.

## Proof Required

- Create `proof/SB20/manifest.md` or an execution-report proof row with changed files, validation commands, and source assertions for this subbundle.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run the relevant component or Playwright tests and capture large-screen plus narrow-width screenshots where layout or provider switching is visible.
- Run component tests and Playwright route smoke for zero-provider and two-mock-provider states.
- Capture screenshots for provider list, provider detail, empty state, and provider error state.

## Browser Validation Logging

- Record route, viewport, Playwright actions, assertions, screenshot paths, and screenshot review questions in `reviews/01-execution-report.md`.

## Progression Gate

- Downstream subbundles may start only after SB20 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Completion Proof

- Proof manifest: `bundle://proof/SB20/manifest.md`
- Semantic invariants: `bundle://proof/SB20/semantic-invariants.md`
- Focused component tests: `bundle://proof/SB20/transcripts/passing-memory-ui-component-tests.txt`
- Playwright route smoke and screenshots: `bundle://proof/SB20/transcripts/passing-memory-ui-playwright-tests.txt`
- Source boundary audit: `bundle://proof/SB20/transcripts/source-boundary-audit.txt`
- Solution build: `bundle://proof/SB20/transcripts/passing-solution-build.txt`
- Closure gate: `Passed`

## Suggested Agent Prompt

```text
Implement subbundle SB20 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
