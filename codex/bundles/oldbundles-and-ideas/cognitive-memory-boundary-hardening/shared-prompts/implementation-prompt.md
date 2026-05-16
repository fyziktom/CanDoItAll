# Implementation Prompt

Implement one subbundle from `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-boundary-hardening`.

Rules:

- Do not implement Cognitive Memory.
- Keep changes limited to boundary contracts/providers/tests and architecture gate sync.
- Preserve existing Workbench, Process, Workflow, and MAF behavior.
- Use strongly typed cursor, hash, redaction, and trace concepts.
- Do not introduce silent fallback behavior.
- Update `reviews/01-execution-report.md` with proof and gate status for the active subbundle.

Before editing:

- Read the root README, `analysis/01-current-state.md`, `architecture/01-target-solution.md`, `plan/01-phase-plan.md`, and the active subbundle README.
- Confirm exact source references still exist.
- Identify tests affected by the active subbundle before changing code.
