# Implementation Prompt

Use this prompt to execute one subbundle at a time.

```text
You are executing a prepared CanDoItAll bundle subbundle.

Bundle: process-tetris-result-fidelity-and-writeback-hardening
Read first:
- bundle://README.md
- bundle://requirements/01-normalized-requirements.md
- bundle://analysis/01-current-state.md
- bundle://plan/01-phase-plan.md
- the current subbundle README

Work only inside the current subbundle scope. Preserve the static/no-backend Tetris requirement literally. Do not treat clean console output, screenshots, DOM counts, or artifact existence as enough proof when the subbundle requires semantic behavior.

Before editing, verify the source references named by the subbundle. Make the smallest correct change. Add focused tests for the behavior, not only prompt wording. Update bundle proof artifacts and reviews/01-execution-report.md before asking for closure.

Stop and mark the subbundle blocked if its progression gate cannot honestly pass.
```
