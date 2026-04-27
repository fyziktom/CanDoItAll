# Codex Prompt: 07 - Provider Capability Matrix and Runtime Gating

You are implementing subbundle `07-provider-capability-matrix` of the CanDoItAll MAF stabilization work.

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

Do not guess capabilities from memory. Inspect installed package behavior and existing provider adapter code. If MAF/provider capabilities are ambiguous, choose a safe failure or explicit opt-in.

## Completion report

Return:

```text
Subbundle: 07-provider-capability-matrix
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
