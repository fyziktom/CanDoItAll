# Forbidden patterns

- do not keep production call sites on `EnqueueTrackedAsync(...)` while calling the runtime plane "done",
- do not keep a bridge worker that only logs queue items if production producers remain,
- do not expose the legacy queue as the plugin-facing scheduling surface.
