# Workflow Run Idempotency

## Status

- `Completed`

## Objective

- Close N005 by connecting public `Idempotency-Key` headers to the existing durable
  workflow launch idempotency owner and adding lookup.

## Success Criteria

- Identical concurrent/post-timeout submissions create one run and return replay
  disposition.
- Reuse with changed workflow/version/backend/canonical input returns 409.
- Lookup returns safe key/hash, original run id, state, and persisted fingerprint evidence.

## Covered Inputs

- N005 / R005.

## Prerequisites

- SB04 stable workflow/version identity closed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\Api\WorkflowsApi.cs`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Models\Workflows\WorkflowLaunchModels.cs`
- `C:\repositories\CanDoItAll\src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Core\WorkflowLaunchService.cs`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.AgentFramework\Persistence\PersistentWorkflowStores.cs`
- `C:\repositories\CanDoItAll\tests\Unit\CanDoItAll.Tests.Unit\WorkflowLaunchServiceTests.cs`
- `C:\repositories\CanDoItAll\tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs`

## Deliverables

- Header validation and canonical launch fingerprint.
- Public bridge to `WorkflowLaunchIdempotency.CallerSupplied`.
- Typed start response with created/replayed disposition.
- lookup-by-idempotency-key route and durable evidence DTO.

## Dependency Impact

- Critical run identity foundation for agent-recruiting typed evidence and SB07/SB08.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical foundation.

## Implementation Steps

1. Characterize existing durable claim/store semantics.
2. Define workspace scope and canonical input/backend/version fingerprint.
3. Map both start routes to caller-supplied idempotency.
4. Add lookup query and typed response/conflict metadata.
5. Run parallel/retry/conflict/authorization tests.

## Scope Exceptions

- No retry of non-idempotent downstream executor side effects outside the existing workflow
  launch contract.

## Do Not Do

- Do not implement endpoint-local locks or caches.
- Do not return a different run for a completed replay.

## Acceptance Checklist

- [x] one run after concurrent identical submissions
- [x] replay response names original run
- [x] changed fingerprint returns 409
- [x] lookup reflects current terminal state
- [x] protected route remains protected

## Proof Required

- Existing core idempotency tests plus new API concurrency/retry tests.
- Exact downstream side-effect count assertion for a deterministic workflow.
- Affected builds and OpenAPI assertions.

## Browser Validation Logging

- N/A.

## C# Architecture Impact

### Boundary Ownership

- Existing workflow launch service/store own claims; Web parses the header.

### Dependency Direction

- Preserve Workflows Core -> Abstractions/Models.

### Pattern Decision

- Reuse existing durable Command/Ledger.

### Testability Contract

- Fingerprint and claim behavior directly tested; HTTP adds binding proof.

### Partial Class Policy

- N/A.

### Architecture Proof Required

- Source assertion that both routes use the same launch service claim path.

## Progression Gate

- Concurrent semantic proof and workflow architecture checkpoint unlock SB06.

## Reopen Triggers

- Duplicate run/side effect, unsafe key disclosure, or changed replay accepted.

## Closure Evidence

- Both public start routes advertise and validate the optional `Idempotency-Key` header,
  delegate to `IWorkflowLaunchService`, and return a typed flattened response with
  created/replayed disposition and only a safe key hash.
- The existing durable ledger remains the concurrency owner. API-origin keys are unique
  within the current workspace database; internal scheduler/process scopes retain their
  prior semantics.
- `GET /api/workflows/runs/by-idempotency-key/{key}` is attached to the authorized API
  group and returns persisted request fingerprint/canonical-input hash, the original run,
  replay evidence, and current terminal state without returning the raw key.
- Migration `20260726030623_AddWorkflowRunIdempotencyEvidence` adds the canonical input
  hash, replay count/timestamp, and filtered unique API-key index. EF reports no pending
  model changes.
- Focused unit proof: 25/25 passed (23 existing launch-service cases plus 2 canonical JSON
  cases).
- Persistent integration proof: 5/5 passed (2 existing ledger/crash-recovery cases plus
  3 API/OpenAPI/auth cases). Eight concurrent identical HTTP submissions produced exactly
  one created response, seven replays, one run id, and one persisted run.
- Core, AgentFramework module, migrations, and Web builds have 0 errors. The only warnings
  are the recorded baseline NU1903 advisory set.
- Closure CodeAnalytics snapshot `snap-20260726032132-5a6a0c3e` covers 5 projects and 267
  documents with no blocking errors. Its module/type cycle node ids exactly match the
  pre-existing pair from SB04, so SB05 adds no cycle.
