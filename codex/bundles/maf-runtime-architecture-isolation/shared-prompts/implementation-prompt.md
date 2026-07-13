# Implementation Prompt

Use this prompt when assigning a subbundle to an implementation agent.

```text
Implement only the selected subbundle from codex/bundles/maf-runtime-architecture-isolation.

Read these files first:
- README.md
- inputs/00-original-request.md
- analysis/01-current-state.md
- requirements/01-normalized-requirements.md
- architecture/01-target-solution.md
- plan/01-phase-plan.md
- traceability/01-requirement-traceability.md
- reviews/01-execution-report.md
- the selected subbundle README

Do not implement Financial Strategist, MarkItDown, document conversion fixes, margin calculation, quotation extraction, or project-structure writeback in this bundle.

Keep changes staged. Extract real collaborators with typed request/result contracts and direct tests. Do not create interfaces unless they enable testing, mocking, or a real boundary. Preserve behavior, access filtering, approvals, credential masking, disposal, diagnostics, and existing extension points.

Before editing, verify prerequisites and current source state. After editing, capture the required proof, update reviews/01-execution-report.md, write proof/SBxx/manifest.md and proof/SBxx/semantic-invariants.md, and stop if the progression gate cannot honestly pass.
```
