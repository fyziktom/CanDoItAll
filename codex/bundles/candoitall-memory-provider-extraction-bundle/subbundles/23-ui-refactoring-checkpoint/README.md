# 23 Ui Refactoring Checkpoint

## Status

- `Completed`

## Objective

- Refactor and harden UI composition, provider-specific surface loading, accessibility/readability, and no-provider fallback before native extraction.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R12
- R13
- R20

## Prerequisites

- SB20-SB22 completed

## Exact Source References

- `bundle://plan/02-checkpoints.md`
- `bundle://architecture/05-ui-composition.md`
- `bundle://architecture/07-testing-and-mocking-strategy.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Audit SB20-SB22 UI code for native assumptions, duplicated state containers, overgrown components, hidden provider defaults, poor empty/error states, and iframe security gaps.
- Extract common UI state, provider selector, operation status components, feedback widgets, and provider surface host helpers into bounded components/services.
- Strengthen component and Playwright tests for zero-provider, mock-provider, provider-specific RCL, iframe, async operation, and feedback flows.
- Ensure screenshots are reviewed for readability, layout hierarchy, alignment, spacing, and responsive behavior.
- Block native UI extraction until the generic UI is provider-agnostic and fallback-safe.
- Verify zero-provider UI is useful for provider management while provider-backed commands are disabled or return typed diagnostics without hidden dispatch.

## Dependency Impact

- Blocks native UI/service extraction if generic UI cannot stand alone.

## Validation Depth

- `Critical UI checkpoint`

## Implementation Steps

1. Run source audit for generic UI references to native Cognitive Memory implementation namespaces.
2. Inspect component sizes and split overgrown components into smaller reusable components with focused tests.
3. Verify all provider-specific tabs/dialogs are declared through provider UI manifests, not hardcoded in generic routes.
4. Run large-screen and narrow-width browser validation for provider list, query/chat, operations, feedback, RCL, and iframe surfaces.
5. Record UI checkpoint findings and reopen SB20-SB22 if fallback or provider isolation is weak.
6. Block SB24 if generic UI requires native Cognitive Memory, Qdrant, OpenAI, or a mock provider to render.

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
- Generic UI can be shipped without native Cognitive Memory installed.
- Provider-specific surfaces can be removed/disabled without breaking the common UI shell.
- Browser evidence covers both zero-provider provider-management rendering and zero-provider command denial/disabled states.
- Browser evidence proves not just route load, but provider switching, fallback, and layout quality.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB23/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB23/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB23/manifest.md` and `proof/SB23/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run the relevant component or Playwright tests and capture large-screen plus narrow-width screenshots where layout or provider switching is visible.
- Capture source audit, component-size review, and screenshot review answers.
- Run Playwright/component tests covering zero-provider, mock-provider, RCL, iframe, async operation, and feedback UI.

## Browser Validation Logging

- Record route, viewport, Playwright actions, assertions, screenshot paths, and screenshot review questions in `reviews/01-execution-report.md`.

## Progression Gate

- Downstream subbundles may start only after SB23 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB23 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
