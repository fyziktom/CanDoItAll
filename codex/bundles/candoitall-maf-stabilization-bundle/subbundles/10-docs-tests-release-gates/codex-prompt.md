# Codex Prompt: 10 - Documentation, Regression Tests, and Release Gates

You are implementing subbundle `10-docs-tests-release-gates` of the CanDoItAll MAF stabilization work.

Read:

- `README.md` in this subbundle
- `../../analysis/current-state-audit.md`
- `../../analysis/repository-evidence-map.md`
- `../../architecture/target-architecture.md`
- `../../requirements/requirements.md`

Then implement the requested changes.

## Execution rules

- Confirm the issue still exists before changing code.
- Make the smallest correct architecture-level change.
- Preserve working process automation behavior.
- Add or update tests.
- Run relevant build/test commands.
- All source-code comments must be in English.
- Do not fake test results.
- If a Microsoft Agent Framework API differs from documentation, adapt to the installed package version and report the difference.

## Specific instructions

This is the closure bundle. Do not mark the stabilization complete if build/tests are not actually run, unless the environment limitation is explicit and unavoidable.

## Completion report

Return:

```text
Subbundle: 10-docs-tests-release-gates
Status: Completed / Partially completed / Blocked
Files changed:
- ...
Tests run:
- command: result
Key behavior changes:
- ...
Remaining risks:
- ...
```
