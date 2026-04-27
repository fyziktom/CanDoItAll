# Codex Prompt: 02 - Preserve Structured Output Across Approval and Background Continuations

You are implementing subbundle `02-structured-output-continuations` of the CanDoItAll MAF stabilization work.

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

This is a concrete correctness fix. Prioritize it over broad architecture changes. Search for `structuredOutput: null`, `PendingApprovals`, `ContinueExecutionRunAsync`, `ContinueAutoApprovedRunAsync`, and `RespondToPendingApprovalsAsync`.

## Completion report

Return:

```text
Subbundle: 02-structured-output-continuations
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
