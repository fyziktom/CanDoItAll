# Shared Implementation Prompt

Use this prompt when executing any subbundle:

```text
You are executing one subbundle from codex/bundles/workflow-node-project-isolation. Do not skip prerequisite gates. Reopen the bundle files before changing code: README.md, plan/01-phase-plan.md, traceability/01-requirement-traceability.md, this subbundle README, and reviews/01-execution-report.md.

Implement only the current subbundle scope. Preserve executor ids, workflow JSON compatibility, template keys, side-effect descriptors, plugin source/trust metadata, deterministic Run Preview behavior, and persisted run/checkpoint/artifact shape unless the subbundle explicitly records a compatibility exception.

Keep boundaries strict:
- workflow abstractions must not reference MAF, Web, Modules, or plugin implementations;
- executor abstractions must not reference MAF or implementation categories;
- MAF must be adapter-only after SB11/SB13;
- plugins must remain first-class executor sources.

Before closure, capture artifact-backed proof for critical subbundles under proof/SBxx/, update reviews/01-execution-report.md, and run the subbundle closure gate.
```

## Coding Constraints

- Prefer small focused project moves over broad refactors.
- Keep classes sealed unless they are intended extension points.
- Do not add fallback behavior that hides missing executor/template/plugin failures.
- Logs and exceptions must include actionable ids and paths while masking secrets.
- Use constants or typed ids for executor ids, template keys, plugin ids, and operation names.
- Do not generate XML documentation comments unless explicitly requested.

## Standard Validation Starting Points

- `dotnet build CanDoItAll.slnx`
- Focused `dotnet test` filters for moved workflow/executor/plugin tests.
- Component tests for workflow UI when UI code changes.
- Playwright tests for workflow shell and project-structure workflow nodes when browser-visible behavior changes.
