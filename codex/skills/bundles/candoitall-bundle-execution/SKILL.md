---
name: candoitall-bundle-execution
description: "Execute or resume a CanDoItAll bundle phase by phase while keeping code, dependencies, proof, status, and raw-input closure aligned. Use when a prepared or compatible bundle exists and Codex must implement it with risk-proportionate validation, including multi-repo, C# architecture, Blazor UI, host, or governed-proof work."
---

# CanDoItAll Bundle Execution

Treat bundle meaning as the contract. Preserve the existing compatible shape and update durable state as implementation proceeds.

## GPT-5.6 Execution Contract

- Work toward the current subbundle outcome; do not re-plan unrelated phases.
- Inspect prerequisite evidence and current source before editing.
- Make the smallest coherent change that satisfies the owned acceptance criteria.
- Use independent retrieval in parallel when useful; keep implementation steps sequential when one result selects the next action.
- Validate the changed behavior at the selected proof tier. Retry weak evidence with a better check; do not summarize around a required gap.
- Stop when the current phase can be proven, or when a concrete blocker prevents proof.

## Entry

1. Read the root status, dependency plan, selected subbundle, its owned raw inputs, and relevant traceability.
2. Map semantic roles if the bundle is non-canonical; do not migrate it just to execute.
3. Reinspect exact source surfaces and nearby tests. Check all affected repositories when the phase crosses repo boundaries.
4. Run `candoitall-subbundle-validator` at the declared proof tier.
5. Stop and repair or reopen when a prerequisite is missing, stale, contradicted, or no longer proves the dependency.

## Delivery Loop

1. Implement only the current coherent outcome.
2. Keep literal scope and explicit exceptions visible; do not silently weaken `all`, `every`, `same flow`, `must`, or equivalent language.
3. Add behavior proof appropriate to the selected tier.
4. Run affected tests/builds and the applicable browser or host checks.
5. Record the result, evidence, closure decision, and downstream progression while proof is fresh.
6. Run the closure gate. Move forward only when it passes or the bundle honestly records a blocker.
7. Reopen earlier work when later evidence invalidates it.

## Proof Tiers

### Standard

- Run the most relevant affected build, analyzer/static check, targeted test, or minimal smoke.
- Record exact commands and results in the execution report. Durable transcript files are optional.

### Behavioral

- Include Standard proof.
- Prove a realistic intended case and a negative, boundary, regression, or failure case that would catch a shallow implementation.
- Record source ownership and user/domain behavior, not only status, counts, non-empty output, or file existence.

### Governed

- Include Behavioral proof.
- Create `proof/SBxx/manifest.md` and `proof/SBxx/semantic-invariants.md` or `.json`.
- Capture required transcripts, hashes, source assertions, anti-stub evidence, browser/host artifacts, and downstream/red-team proof.
- Use portable references where the bundle can resolve them. Record repo and active-skill hashes when skill instructions or validators change.

Read [references/semantic-adequacy-proof.md](references/semantic-adequacy-proof.md) for Behavioral/Governed proof and [references/artifact-backed-proof-manifest.md](references/artifact-backed-proof-manifest.md) only for Governed proof.

## Validation Rules

- Prefer targeted tests for changed behavior, type/analyzer checks when applicable, affected-package builds, and a minimal smoke when full validation is too expensive.
- A missing full-solution build does not block a low-risk phase when affected-scope validation is sufficient and the report explains the boundary.
- Missing evidence that is required by the selected tier is a blocker or reopen condition.
- Production-only behavior must be proven through its real producer/lifecycle path when that behavior is in scope; manually seeded consumer tests are supporting evidence only.
- Use Microsoft Testing Platform hot reload only for iteration; finish with a clean confirmation run.

## CanDoItAll UI Rule

- Use Playwright/browser truth and `candoitall-components-mcp` for CanDoItAll Blazor work. Read its `references/compact-ui-composition.md` before changing composition; use shared components and semantic tokens before custom structural markup or CSS.
- Validate CanDoItAll applications at a maximized or named large-screen desktop viewport. Do not tune or validate small, medium, tablet, or mobile application layouts unless explicitly requested.
- For reusable basic `CanDoItAll.Components.BaseLib` work, validate small, medium, and large viewports.
- For other shared libraries, preserve existing responsive behavior when touched; new responsive work requires explicit scope.
- Confirm that the implemented primary surface, supporting-content placement, stats treatment, list/editor organization, textarea/dialog sizes, first-viewport target, and scroll owner still match the bundle decision.
- Inspect normal-state and open-overlay screenshots rather than merely attaching them. Check readability, hierarchy, clipping, scroll ownership, spacing, interaction states, and consistency.
- Open menus, tooltips, dropdowns, dialogs, floating windows, and overlays; capture the relevant open state and prove readable content, correct layering, visible actions, usable internal scrolling, and no harmful clipping/overflow at the target viewport.
- Browser proof does not replace host-level proof for process launch, file opening, elevation, or OS integration.

Read [references/ui-validation-questions.md](references/ui-validation-questions.md) for the applicable viewport checklist.

## C# Architecture Rule

For architecture-relevant work:

- read the prepared ownership, boundary, dependency, pattern, testability, and checkpoint artifacts that exist;
- use CodeAnalytics before and after dependency-direction or large-class changes when available;
- do not add unplanned project references or partial-class expansion;
- stop on cycles and extract a smaller contract;
- prove extracted behavior through the new owner without requiring the old large runtime;
- update the architecture record and run `csharp-architecture-review-gate` before closure.

## Durable Updates

After each completed, blocked, or reopened subbundle, update the bundle’s semantic equivalents of:

- root execution/validation state;
- subbundle status and progression decision;
- execution report and proof paths;
- raw-input closure;
- blockers/follow-ups and downstream gates that must be rechecked.

For UI work, also update the execution report's composition review and record first-viewport, scroll-owner, and open-overlay findings while the screenshots are visible.

If the bundle contract materially changes, rerun canonical prepared validation or the recorded manual compatibility gate. If bundle skills or validators change, synchronize the active skill root and verify hashes before relying on them.

## Stop Rules

- Stop current implementation when scope or dependencies materially diverge; repair the bundle first.
- Stop downstream execution when prerequisite proof becomes untrusted.
- Stop investigation when the phase has enough evidence to pass its selected tier.
- Do not manufacture extra subbundles, evidence files, responsive passes, or architecture layers without a risk or dependency reason.

## References

- Read [references/execution-loop.md](references/execution-loop.md) for the concise phase loop.
- Read [references/proof-and-status-updates.md](references/proof-and-status-updates.md) before closure updates.
- For UI execution, read `candoitall-components-mcp/references/compact-ui-composition.md`.
- Use `candoitall-subbundle-validator` for entry/closure and `candoitall-bundle-validator` for final closure.
- Activate architecture, Components MCP, Playwright, screenshot, or host skills only when the current phase needs them.

## Exit Condition

Execution is complete when code, affected-scope validation, applicable rendered/host proof, progression decisions, raw-input closure, and durable bundle status agree at the selected proof tiers.
