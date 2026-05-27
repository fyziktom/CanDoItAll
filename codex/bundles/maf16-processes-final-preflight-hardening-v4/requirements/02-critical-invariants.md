# Critical Invariants

1. Read model must never display `Satisfied` for an artifact that finalizer validation would reject.
2. Every finalizer validation status must have an API/UI-visible mapped status or diagnostic.
3. A package upgrade is not the same as feature adoption.
4. Deferred MAF features must have a documented reason and safe fallback.
5. Agent tool calls must always pass through CanDoItAll policy.
6. Processes remain above Workflows.
7. A full live process test starts only after step0 live smoke proves artifact validation and read model agree.
