# Readiness gate

Do not accept the implementation unless all mandatory gates pass.

## Gate 1 — Finalizer runtime mode alignment

Mandatory:

- Required mode attaches finalizer tool and required instructions.
- Shadow mode does not require exact-one finalizer in runtime instructions.
- Disabled mode attaches no finalizer tool and no finalizer instructions.
- Initial run, approval continuation, auto-approval continuation, and temperature retry all preserve effective finalizer mode.

## Gate 2 — Structured-output instruction consistency

Mandatory:

- Required mode finalizer instructions are compatible with JSON-schema `ResponseFormat`.
- Required mode final assistant response must be JSON, not Markdown/prose.
- No “display-only assistant text” instruction remains in required structured-output mode.

## Gate 3 — Tool-policy exception boundary

Mandatory:

- A dedicated policy-block exception exists.
- Actual tool exceptions are not misclassified as policy blocks.
- Tests cover both policy blocks and real tool exceptions.

## Gate 4 — Provider capability truth

Mandatory:

- Core feature matrix, UI defaults, registry persistence, and managed provider display no longer contradict each other.
- Ollama local/remote do not default to structured-output capable unless proven by a tested implementation.

## Gate 5 — Tests and commands

Mandatory commands:

```bash
dotnet --info
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --configuration Release --no-restore
dotnet test CanDoItAll.slnx --configuration Release --no-build
```

If commands cannot run, document exactly why.

## Gate 6 — Documentation truthfulness

Mandatory:

- Verification documentation states exact commands run.
- It does not claim passing tests if tests were not executed.
- It documents any remaining limitations.
