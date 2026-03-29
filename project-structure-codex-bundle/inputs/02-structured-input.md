# Structured Input

## Request Shape
- Bundle-driven implementation and validation run.
- UI-heavy and architecture-heavy refactor.
- Cross-surface shared canvas risk exists for ProjectStructure, PromptFactory, and Sandbox.

## Hard Constraints
- Preserve mapped functionality from `02_FEATURE_PRESERVATION_MAP.md`.
- Follow the ordered task sequence from `codex/TASK_SEQUENCE.md`.
- Use plain JavaScript only for the hot path.
- Use real Playwright MCP validation and screenshots for UI-visible tasks.
- Treat a failed test, browser gate, or performance gate as an open subbundle.

## Observable Success
- Overlay isolation, viewport ownership, batching, reload reduction, retained rendering, and testability all improve.
- Browser-visible behavior remains intact across ProjectStructure and shared canvas consumers.
- Bundle execution state, gate decisions, analytics, and closure notes remain synchronized with the repo.

## Source Coverage Notes
- The original audit pack was static and did not include execution logs.
- This repaired bundle must generate the runtime evidence that the original audit intentionally lacked.
