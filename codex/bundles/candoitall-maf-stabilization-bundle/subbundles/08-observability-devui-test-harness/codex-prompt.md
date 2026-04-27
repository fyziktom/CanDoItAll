# Codex Prompt: 08 - Observability, Tracing, and Agent Test Harness

You are implementing subbundle `08-observability-devui-test-harness` of the CanDoItAll MAF stabilization work.

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

Prioritize stable diagnostic schema over excessive logging. Keep raw output logging hashed/redacted unless existing policy explicitly allows raw content.

## Completion report

Return:

```text
Subbundle: 08-observability-devui-test-harness
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
