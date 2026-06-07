# Assumptions And Risks

## Assumptions
- The branch `maf-processes-refactor` is the source of truth.
- The latest Core stabilization bundle was applied as pushed.
- The next implementation agent can run solution build, full unit tests and focused integration tests locally.
- Runtime/service refactor remains backend-only; UI/browser validation is N/A unless source changes unexpectedly touch UI files.

## Critical Path Risks
- **Core creep**: deterministic evidence descriptors may accidentally pull execution/finalizer/application behavior into Core.
- **Driver API creep**: helper-driver vocabulary may become production contracts before permission/audit/sandbox ownership is ready.
- **Adapter leakage**: Core consumers may bypass explicit module adapters or hide source payloads.
- **Warning normalization**: build warning cleanup can be treated as unrelated and skipped, weakening future gates.
- **Proof weakness**: broad documentation can make progress look larger than source changes. Each phase needs executable source scans/tests.
- **Side-effect confusion**: verification-only drivers must not mutate process state, write artifacts, run shell commands, call Office/Graph, or trigger retries.

## Validation Risks
- Full integration tests can be slow. Require focused integration matrix plus attempted broad integration if feasible.
- Architecture tests must be active-bundle aware and must not keep checking old bundle paths only.
- Public Core API snapshots must be updated intentionally for every new public type/member.

## Reopen Triggers
- Any `CanDoItAll.Processes.Core` source references EF, Infrastructure, Modules, AgentFramework, storage/workspace/filesystem, logger, service provider, finalizer application, claim, transition execution, or driver API tokens.
- Any production source introduces `IProcessDriver*`, driver registry, DI registration, manager command, runtime selector, or execution-capable helper.
- Any Core public API changes without the public API inventory and architecture snapshot update.
- Any route/subprocess/artifact parity test fails.
- Any build warning is newly introduced by this bundle or the current 3-warning baseline is not explicitly closed or deferred.

