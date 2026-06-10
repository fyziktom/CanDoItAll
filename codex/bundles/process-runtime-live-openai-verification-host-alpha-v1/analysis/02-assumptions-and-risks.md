# Assumptions And Risks

## Assumptions
- Branch `maf-processes-refactor` remains the active branch.
- Previous release-candidate deterministic runtime proof remains valid unless the baseline gate detects drift.
- OpenAI key is available in the local environment or configured secret store, but the key value must never be printed.
- The first runtime host alpha is verification-only and can be implemented with explicit registry/selector over known lanes.

## Critical Path Risks
- The verification host accidentally becomes an execution-capable host by adding shell/file/network/storage/process mutation APIs.
- DI registration accidentally becomes auto-discovery and permits unapproved drivers.
- Selector fallback accidentally permits wrong-lane execution.
- Manager command accidentally applies recovery/finalizer/transition rather than only returning diagnostics.
- Live OpenAI smoke leaks secrets or becomes nondeterministic/flaky without budget/timeout.
- Audit persistence is added without redaction/hash proof.
- Scheduler/workflow integration skips process services and starts calling drivers directly.
- Process Core begins referencing driver abstractions, modules, infrastructure, UI, EF, or AgentFramework.

## Validation Risks
- Deterministic tests may hide provider errors.
- Live smoke may fail for provider/quota/transient reasons; failure must be diagnostic, not secret-leaking.
- Unit tests can pass while UI process launch or manager command UI/API is broken.
- Source scans must distinguish denied roadmap text from actual runtime host implementation.

## Reopen Triggers
- Any concrete `codex/bundles/<bundle-name>` path reappears under `src` or `tests`.
- Live OpenAI smoke is skipped while key is present and no explicit opt-out exists.
- Verification host exposes an execution method, command runner, external call, storage/workspace writer, process mutator, claim/transition/finalizer/retry mutator, or unbounded object payload.
- Core references driver packages.
- Process manager diagnostics mutate process state.
- Scheduler/workflow hooks call driver host directly before the future execution-capable gate is approved.
