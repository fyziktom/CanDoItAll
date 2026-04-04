# Normalized Requirements

- `RQ-01`: Workbench delete and subtree-transfer flows must no longer leave the graph committed if CRM/HR assignment reconciliation fails.
- `RQ-02`: The cross-module canonical node-scoped bridge must stop using raw strings where a typed node reference can express intent more safely.
- `RQ-03`: Workbench metadata must stop persisting canonical-looking party identifiers for participant, meeting, and work-item projection state.
- `RQ-04`: Display-side summaries used by the structure UI must continue to work after metadata cleanup.
- `RQ-05`: Architecture guardrails for canonical ownership, projection-only metadata, and Workbench-node extension strategy must be recorded explicitly.
- `RQ-06`: Targeted build, component, integration, and browser validation must prove the next-wave changes did not regress the repaired flow.
