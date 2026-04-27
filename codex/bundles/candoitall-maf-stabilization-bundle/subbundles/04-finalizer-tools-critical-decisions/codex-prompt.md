# Codex Prompt: 04 - Finalizer Tools for Critical Decisions

You are implementing subbundle `04-finalizer-tools-critical-decisions` of the CanDoItAll MAF stabilization work.

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

Start with a narrow target. Do not replace all structured final responses at once. Prefer a shadow-mode comparison for `ProcessStepOutcomeResult` if that reduces risk: capture both structured response and finalizer result, compare them, then decide whether to switch full mode after tests are stable.

## Completion report

Return:

```text
Subbundle: 04-finalizer-tools-critical-decisions
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
