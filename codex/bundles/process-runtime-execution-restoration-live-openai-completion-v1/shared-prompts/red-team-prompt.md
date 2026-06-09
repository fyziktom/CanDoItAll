# Red-Team Prompt

Reject the bundle if any of these occur:
- report-only completion;
- non-empty diagnostics treated as process execution proof;
- UI launch proof without persisted run readback;
- process run created but not dispatched/finalized;
- live OpenAI skipped but reported as provider success;
- deterministic fake scenario used as live proof;
- driver path mutates process state;
- Core imports driver/module/infrastructure/runtime dependencies;
- tests read concrete `codex/bundles/<bundle-name>` paths;
- runtime-host approval is implied by roadmap prose.
