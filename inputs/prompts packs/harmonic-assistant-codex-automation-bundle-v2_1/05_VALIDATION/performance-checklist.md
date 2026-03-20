# Performance Checklist

- Avoid per-frame allocations in canvas where possible (reuse arrays/maps).
- Cache text measurements when labels unchanged (optional).
- Clamp zoom and retry logic to max 3 passes.
- Throttle .NET callbacks (widget events are discrete, not continuous).
