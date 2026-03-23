# ProjectCalendarAdapter Checklist

## Implementation checklist

- [ ] Reviewed all referenced files before coding.
- [ ] Created or extracted the component in the recommended path.
- [ ] Reused existing shared contracts or domain services where appropriate.
- [ ] Removed or reduced the old ownership leak.
- [ ] Updated tests.

## Validation checklist

- [ ] Happy path works.
- [ ] Edge cases from the specification were checked.
- [ ] Error fallback was verified.
- [ ] Interop contract was validated if applicable.

## UX/UI checklist

- [ ] Visual hierarchy is clear.
- [ ] Selection/hover/focus/read-only states are visible and coherent.
- [ ] Truncation, image, or overlay behavior is intentional where applicable.
- [ ] Keyboard path exists for critical actions where relevant.

## Architecture checklist

- [ ] Shared vs domain-specific ownership is correct.
- [ ] Low-level vs high-level responsibility is clear.
- [ ] No duplicate abstraction was added.
- [ ] The component fits the target architecture and wave plan.

## Performance checklist

- [ ] No avoidable full-surface rebuild in hot paths.
- [ ] Expensive measurement or geometry work is cached/batched when relevant.
- [ ] Interop calls are coarse-grained when relevant.
- [ ] Large-scene or dense-data behavior was considered when relevant.
