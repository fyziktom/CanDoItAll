# Code vs Bundle Churn

The previous bundle was more efficient than older proof-heavy bundles, but still too close to parity between implementation and bundle content.

Final closure for this bundle must treat only `src` and `tests` as implementation. `docs` do not count toward the implementation ratio.

Required final ratio:

```text
(src + tests changed lines) >= 5 × (codex/bundles changed lines)
```

Execution should update only:
- `reviews/01-execution-report.md`
- one compact proof manifest per critical closure if needed
- concise transcript paths

Do not create new boilerplate subbundle folders or long proof trees during implementation.
