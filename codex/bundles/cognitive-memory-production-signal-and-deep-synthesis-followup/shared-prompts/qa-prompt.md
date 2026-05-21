# QA Prompt

Review the executed bundle as an adversarial QA reviewer. Specifically look for:

- consumer-only implementation without production producer,
- tests that seed the event/state being claimed as production output,
- aggregate memories that store evidence meta-text instead of useful knowledge,
- professor anchors stuck in `Comparing`,
- Czech/diacritic professor capture failures,
- recall briefs that use title/summary instead of the actual query,
- statement source maps that are broad Cartesian products,
- clustering tests that require exact shared keys instead of true semantic candidate discovery,
- service files that grew larger without a boundary refactor.

Fail the bundle if any critical behavior is proven only by prose, grep, or test seeding.
