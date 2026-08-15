# Session handoff — SB03

State: **Completed — SB04 unlocked**

## Entry checklist

- [x] Root bundle status read
- [x] Dependencies complete and proof trusted
- [x] Actual repository/branch/head recorded
- [x] Current source and nearby tests inspected
- [x] Test budget understood
- [x] Database/dependency mode recorded

## Work performed

- Wrapped all three public LLM Chat application interfaces in one operation-scope runner that captures
  the canonical host-root identity before the first application read.
- Reused that outer scope in the conversation engine so repositories, transcript commands, provider
  resolution, and invocation audit share one identity.
- Added an atomic runtime write fence that total-orders durable commits with profile-switch publication.
- Required every root EF LLM Chat transaction to pass the captured identity through that fence.
- Made stale old-root lease acquisition fail even after the runtime state observes a new selected profile.
- Added deterministic public-use-case, fence-ordering, and real-host PostgreSQL provider/finalization tests.

## Files changed

- Infrastructure runtime switching and registration.
- LLM Chat application scope runner, internal interface decorators, typed error, and DI registration.
- LLM Chat persistence lease factory, commit-fence adapter, unit of work, engine scope reuse, and DI.
- Focused Unit and Integration tests plus governed bundle proof.

## Commands and results

- Pre-SB03 historical regression at `61abf5bc3`: exit 1, expected 0 passed / 1 failed / 0 skipped.
- Focused Unit profile/runtime/composition filter: exit 0, 12 passed / 0 failed / 0 skipped.
- Focused real-host PostgreSQL switch-before-finalization test: exit 0, 1 passed / 0 failed / 0 skipped.
- Affected Unit project build: exit 0, 0 warnings / 0 errors.
- `git diff --check`: exit 0.
- CodeAnalytics snapshot `snap-20260815020112-e34a58a8`: no blocking diagnostics, no new
  project cycle, and no LLM Chat layering/service-registration warning.

## Bugs discovered and resolved

- The first focused API compile exposed a missing Ports import in the test wrapper.
- The next compile exposed a wrong usage property name; the assertion now targets `CachedInputTokens`.
- The broad pre-existing real-host scenario incorrectly continued using a host after simulated restart;
  it was split so normal API behavior and stale-host rejection are asserted independently.

## Deviations

The focused API command needed two compile-correction reruns and one sandbox diagnostic rerun before
the exact outside-sandbox PostgreSQL command passed. The historical proof first encountered sandboxed
NuGet.Config access and was rerun unchanged outside the sandbox. No broad suite was run.

## Acceptance result

- [x] Every public LLM Chat application operation captures profile identity before its first read.
- [x] All repositories, provider resolution, transcript commands, and audit writes use the captured operation scope.
- [x] A profile switch prevents every subsequent old-generation durable commit.
- [x] A switch during provider execution yields deterministic non-success or RecoveryRequired with retained usage evidence.
- [x] No current-profile DbContext or provider lease is cached across operations.

## Architecture result

- [x] Owner moved or strengthened as planned
- [x] Old shallow path removed/unreachable
- [x] Direct tests target the new owner
- [x] No forbidden reference/cycle/partial expansion
- [x] Architecture record updated

## Progression

Ready. SB04 is unlocked. Reopen SB03 if a new public LLM Chat service bypasses the interface decorators,
runtime switch semantics change, or a later dispatcher/stream holds a stale scope after terminal closure.
