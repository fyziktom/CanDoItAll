# SB015 Semantic Invariants

- Parent step start, block, completed, failed, and cancelled transitions still use `ProcessSubprocessLifecycleRules`.
- Subprocess artifact projection preserves explicit source artifact resolution and gap journaling.
- Projection writes remain application-local and claim-held guarded.
