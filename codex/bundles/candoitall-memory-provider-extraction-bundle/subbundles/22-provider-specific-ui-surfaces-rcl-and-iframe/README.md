# 22 Provider Specific Ui Surfaces Rcl And Iframe

## Status

- `Completed`

## Objective

- Add UI surface registry and rendering for provider-specific RCL components, iframe surfaces, external links, and fallback behavior.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R13
- R16

## Prerequisites

- SB20 completed

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemoryProbeWorkbenchTab.razor`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemoryClusterSearchTab.razor`
- `bundle://architecture/05-ui-composition.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Add provider-specific UI projection contracts for Blazor/RCL components, iframe/external URL surfaces, provider-declared tabs, dialogs, and advanced operation entry points.
- Move native-specific cluster/probe/review/professor surfaces behind provider-specific UI declarations instead of hardcoding them into the generic shell.
- Support secure iframe configuration with allowed origin, route, sizing, loading/error state, and disabled fallback.
- Support Blazor-compatible provider RCL registration where a provider package can expose advanced tabs/dialogs.
- Add tests with mock RCL and mock iframe provider surfaces, plus a native placeholder surface.

## Dependency Impact

- Native rich UI extraction and optional provider UIs depend on surface projection.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Define provider UI manifest model with surface id, surface type, tab order, route/component id, required capability, and security policy.
2. Refactor native UI tabs from current Cognitive Memory page into provider-specific declarations or mark them for native service UI migration.
3. Implement RCL surface resolver and iframe renderer with safe fallback and provider-disabled behavior.
4. Add browser proof for RCL surface, iframe surface, missing surface, and provider-specific capability mismatch.
5. Document the contract for future memory providers to add rich UI without changing the generic shell.

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
- Generic UI remains useful when provider-specific UI is absent.
- Native advanced surfaces are optional provider extensions, not generic module dependencies.
- Iframe/external UI surfaces are policy-controlled and fail safely when origin or provider state is invalid.

## Proof Required

- Create `proof/SB22/manifest.md` or an execution-report proof row with changed files, validation commands, and source assertions for this subbundle.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run the relevant component or Playwright tests and capture large-screen plus narrow-width screenshots where layout or provider switching is visible.
- Run provider-specific UI tests for RCL, iframe, unavailable provider, missing capability, and disabled surface.
- Capture screenshots for generic fallback, RCL tab, iframe tab, and error state.

## Browser Validation Logging

- Record route, viewport, Playwright actions, assertions, screenshot paths, and screenshot review questions in `reviews/01-execution-report.md`.

## Progression Gate

- Downstream subbundles may start only after SB22 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB22 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
