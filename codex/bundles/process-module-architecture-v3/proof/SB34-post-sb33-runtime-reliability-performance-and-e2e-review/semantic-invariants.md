# SB34 Semantic Invariants

- Dispatch queues are bounded. Queue saturation waits explicitly instead of growing memory without limit.
- Queue capacities are positive configuration values and invalid values fail at construction time.
- A canceled or failed enqueue releases the queued run-id marker before propagating the cancellation or failure.
- Duplicate pending run IDs remain suppressed until the queued item is dequeued.
- The active-run gate stays separate from the pending-queue dedupe gate.
- The Process runtime, dispatcher, builder, manager, projections, and driver contracts remain generic. No TetrisGame or scenario-specific rule was added to production runtime/application files.
- Process adapter regexes use `[GeneratedRegex]` with culture-invariant semantics.
- The existing strategy-invocation isolation timeout remains in place because it protects the dispatcher from strategies that block before returning a task.
- Project-structure e2e setup uses public typed APIs and a project lease, not direct database edits.
- Retry and child-run outcomes remain explicit in the run hierarchy. A recovered parent completion must still be backed by current accepted run evidence.
- Runtime cleanup must leave no generated TetrisGame host process running.
- Generated TetrisGame output must build and test independently after the process completes.
