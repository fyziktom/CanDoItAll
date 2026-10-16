# P01a: reliable release after transaction cleanup failure

Status: entry accepted after independent owner and UI/validation review. Proof tier: Behavioral.

## Outcome and boundary

Repair acquired process-lock leakage in the existing Infrastructure
`SerializableMutationScope` when transaction disposal throws, both during failed
acquisition and normal disposal. Keep the original transaction isolation, key ordering,
stripe count and public API. This is the smallest independently safe prerequisite.
It does not claim commit-outcome or Projects lost-update protection.

Owned inputs: R2/R3/R4/R6; architect transaction cleanup risk/tests T011/T017.
Sources: `src/Foundation/CanDoItAll.Infrastructure/Persistence/SerializableMutationScope.cs`
and `tests/Components/CanDoItAll.Tests.Components/SerializableMutationScopeTests.cs`.

## Acceptance

- `finally` releases all acquired process locks even if the transaction disposer fails.
- Successful/cancelled acquisition still releases only locks actually acquired.
- Disposal stays idempotent; a second disposal cannot over-release semaphores.
- Failure is visible; no blanket exception swallowing or silent fallback.
- When acquisition and transaction cleanup both fail, preserve both exceptions with
  the acquisition failure first; keep the original type when only one operation fails.
- Tests inject disposal failure through supported EF transaction services/interceptors,
  not production test switches or private reflection.
- A successor operation with the same scope key can acquire and complete.

## Architecture impact

Infrastructure keeps resource lifecycle mechanics. No contract extraction, new project,
interface, DI registration or partial class is justified. Existing helper remains cohesive.
No Projects/runtime behavior belongs in this repair. The test must exercise the helper
directly, not construct the full application.

## Validation

Build Infrastructure Release, then refresh the Components test assembly and discover
`FullyQualifiedName~SerializableMutationScopeTests`. Existing baseline is one test.
Expected final count: existing serialization test plus four reviewed fault/normal cases
described in `reviews/owner-design-review.md`; finalize exact data rows before execution
and record the count. The successor acquisition in each failure case is the dependent-use
proof. Use a test-only EF `IRelationalTransactionFactory` replacement and the isolated
PostgreSQL fixture; test the released local stripe with an InMemory contender using the
same scope key, so releasing a database lock cannot hide a local semaphore leak.

Run portability-static (including tooling/rendering checks), inspect any added/stale
findings, then require final no-write enforcement. The shared persistence helper is a
named broader invalidation input; its broad stable checkpoint is scheduled at the
integrated frozen foundation, not after this small failure-path commit.

Reopen if fault injection reveals incorrect primary-error classification or changes
normal transaction semantics. Closure records exact commands/discovery/results and
independent diff review. Commit only after this slice passes its focused checks.
