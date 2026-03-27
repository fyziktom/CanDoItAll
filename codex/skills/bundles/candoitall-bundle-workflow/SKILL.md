---
name: candoitall-bundle-workflow
description: "Run the end-to-end CanDoItAll bundle workflow: decide whether a bundle must be prepared, prepare it when needed, validate it, then execute it phase by phase until completion. Use when the user wants one commandable workflow instead of manually switching between bundle preparation and execution."
---

# CanDoItAll Bundle Workflow

Use this as the coordinator skill. It keeps the preparation and execution halves aligned and prevents jumping into code before the bundle is ready.

This skill is the right entry point when the user says “prepare a bundle and execute it,” or when the task already smells too broad for direct implementation.

## Workflow

1. Decide whether a usable bundle already exists.
2. If not, switch into bundle preparation mode and create one.
3. Validate the bundle before touching implementation code.
4. Execute the bundle one subbundle at a time.
5. Keep the bundle updated with proof and residual risks.
6. Stop only when the requested scope is implemented and validated, or when a real blocker is documented.

## Decision Rule

- If the user provides raw notes, docx feedback, screenshots, or a broad initiative prompt, start with `candoitall-bundle-preparation`.
- If the user points at an existing bundle and asks to implement it, start with `candoitall-bundle-execution`.
- If the bundle exists but is obviously stale, incomplete, or inconsistent with the repo, repair the bundle first, then execute it.

## Coordination Rules

- Do not start implementing from raw user notes when the work clearly needs decomposition.
- Do not keep a bundle frozen when execution reveals missing proof or incorrect assumptions.
- Do not let execution drift away from the documented bundle.
- Prefer one good bundle that is kept current over many partial bundles.

## UI Rule

When the bundle or subbundle is UI-heavy:

- use `frontend-skill` when available for layout critique and stronger UI validation questions
- use `candoitall-watch-playwright-loop` for nearby-edit browser validation
- keep screenshots and browser proof tied to the subbundle that changed the UI

## References

- Read [references/workflow-decision-tree.md](references/workflow-decision-tree.md) when choosing between preparation and execution.
- Read [references/handoff-rules.md](references/handoff-rules.md) to keep the bundle structure and execution flow compatible.

## Exit Condition

The workflow ends only when the bundle is ready, the implementation is complete, the proof is recorded, and the remaining risk is honestly documented.
