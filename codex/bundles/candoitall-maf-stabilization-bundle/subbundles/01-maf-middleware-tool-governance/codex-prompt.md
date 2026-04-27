# Codex Prompt: 01 - MAF Middleware and Tool Governance Hardening

You are implementing subbundle `01-maf-middleware-tool-governance` of the CanDoItAll MAF stabilization work.

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

Focus on MAF-native middleware. Inspect the installed `FunctionInvocationContext`/middleware API before implementing. If exact APIs differ from documentation, use the installed API and document the difference. The repeated tool guard can remain as a fallback, but the preferred enforcement path must be pre-execution.

## Completion report

Return:

```text
Subbundle: 01-maf-middleware-tool-governance
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
