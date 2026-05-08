# Implementation Prompt

Implement the current subbundle only. Keep changes focused on process runtime observation performance, preserve process execution semantics, and record timing evidence in `reviews/01-execution-report.md`.

Use these rules:

- Prefer batched strongly typed read models over UI-side filtering of full detail models.
- Do not change dispatch state transitions unless measurement proves dispatch is the bottleneck.
- Keep Blazor components thin; put process DB reads behind `ProcessesService` and `IProcessRuntimeReadQueryService`.
- Add targeted tests for the new read model and run relevant process tests.
- Update subbundle status and gate rows as proof is captured.
