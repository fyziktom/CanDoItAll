# SB04 Semantic Invariants

- `CanDoItAll.Git` is process-neutral and does not reference Process runtime/template types.
- Git commands are represented as typed argument lists, not shell command strings.
- Sensitive Git command arguments are masked in sanitized logs.
- Repository paths are authorized against a typed root before Git operations.
- `CanDoItAll.Processes.Templates` does not call Git directly.
- JSON document models are the canonical template source.
- Markdown, Mermaid, import envelopes, and compatibility reports are projection kinds with source hashes, not canonical input.
- Template migrations are planned through ordered adjacent schema versions and fail on missing intermediate migrations.
- Local overrides produce explicit conflict records when global changes touch the same JSON pointer.
