# Assumptions and risks

## Assumptions

- The branch is `maf-processes-refactor`.
- Previous route/model decoupling proof is trusted but must be revalidated by source scans before new work begins.
- The goal remains refactoring and architecture hardening only.
- Existing process behavior, templates, workflow integration, agent execution, artifacts, recovery, subprocess, materialization, and finalizer behavior must remain intact.

## Critical path risks

1. **Wrapper-only progress**: Codex may move methods into new files while keeping the dispatcher as the actual god service.
2. **Behavior drift**: route order, finalizer order, recovery/retry behavior, and subprocess artifact projection can easily change if code is moved carelessly.
3. **Premature Core**: moving types into a Core project before adapters are clean could freeze bad boundaries.
4. **Driver API leakage**: the driver idea is important, but production driver APIs are still too early.
5. **Test masking**: broad tests may be skipped because of known unrelated historical bundle fixture issues; focused tests must be precise and supplemented by source scans.

## Validation risks

- Existing broad architecture tests may still contain stale fixture assumptions. Do not use that as an excuse to skip focused architecture guards.
- If any behavior has no test coverage, add characterization tests before moving it.
- If source scans are too broad or too weak, they can pass while wrappers remain unchanged. Scans must target concrete forbidden patterns.

## Reopen triggers

Reopen the current or prior phase if any of these happen:

- Any existing process workflow loses behavior.
- Route stage order changes.
- Process Core or production driver API appears in source.
- Any UI/mobile/screenshot proof appears for this runtime-only refactor.
- A route/finalizer/subprocess/candidate service simply wraps dispatcher without reducing dispatcher ownership.
- Execution report collapses subbundles or omits gate proof.
- Tests are replaced by source scans only for behavior-moving subbundles.
