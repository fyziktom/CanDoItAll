# Workflow launch idempotency proof

## Architecture

- `IWorkflowLaunchIdempotencyStore` owns atomic claim, lease renewal, completion, and release semantics behind strongly typed scope, fingerprint, claim token, and reserved run identity contracts.
- A scope partitions a normalized caller key by workflow selection, launch mode, and stable origin lineage. Reusing the same scope with a different request fingerprint fails explicitly.
- Each claim persists a unique reserved `WorkflowRunId` before runtime execution. Lease takeover reuses that identity, and runtime start-or-get plus duplicate-create recovery prevents a second workflow run after a process interruption.
- Definition resolution remains in `WorkflowLaunchService`; requested-run recovery is isolated in `WorkflowRequestedRunRecovery`; PostgreSQL persistence is isolated in `PersistentWorkflowLaunchIdempotencyStore`.
- `WorkflowRuntimeManagerRunLauncher` preserves both the caller idempotency contract and reserved run id in `WorkflowRunStartRequest`.
- Failed launches release claims only when no run was persisted. A failure after runtime persistence completes the claim against the existing run so retries replay it.

## Persistence

- Migration: `20260712215655_AddWorkflowLaunchIdempotency`, after `20260712210953_AddProcessWorkflowExecutorBinding`.
- Unique scope index: `UX_AF_WorkflowLaunchIdempotency_Scope`.
- Unique reserved-run index: `UX_AF_WorkflowLaunchIdempotency_Run`.
- Lease scan index: `IX_AF_WorkflowLaunchIdempotency_Lease`.
- Missing-row races after concurrent release are retried instead of using `SingleAsync` assumptions.

## Verification

- `dotnet build src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/CanDoItAll.AgentFramework.Workflows.Runtime.csproj -v:minimal`: 0 warnings, 0 errors.
- `dotnet build src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj -v:minimal`: 0 warnings, 0 errors.
- `dotnet build tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -v:minimal`: 0 warnings, 0 errors.
- Focused launch unit suite before the concurrent contribution-API edit: 20/20 passed. Coverage includes eight-way concurrent replay, request conflict, pre-persistence failure release, post-persistence failure replay, and latest-active version stability.
- Broader launch/usage/runtime/line-budget unit filter before the concurrent contribution-API edit: 43/43 passed.
- Real PostgreSQL `WorkflowLaunchIdempotencyPersistenceIntegrationTests`: 2/2 passed after final cleanup. Coverage includes eight-way atomic claims, twelve concurrent release/reclaim rounds, conflict rejection, expired-lease recovery, same reserved run identity, and zero backend reinvocation.
- EF idempotent migration script from `20260712210953_AddProcessWorkflowExecutorBinding` to `20260712215655_AddWorkflowLaunchIdempotency`: exit 0; table and all three indexes present.
- `WorkflowRuntimeManager.cs`: 748 lines, below the enforced 750-line architecture budget.
- `git diff --check`: no whitespace errors; only repository line-ending notices.

## Final shared-tree verification

- The contribution API converged and the broader workflow/plugin/settings/shared-operations/process unit selection passed 526/526.
- The solution build passed with 0 warnings and 0 errors.
- No shared-tree verification dependency remains for the idempotency implementation.
