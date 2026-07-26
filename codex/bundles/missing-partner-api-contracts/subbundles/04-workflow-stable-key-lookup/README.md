# Workflow Stable-Key Lookup

## Status

- `Completed`

## Objective

- Close N004 by exposing system template provenance and partner external identity without
  display-name matching.

## Success Criteria

- Template-key lookup resolves exact materializations and runnable version ids.
- Catalog/detail expose pack key/version, source hash, template key, and external identity.
- Multiple or stale materializations are explicit, never silently selected.

## Covered Inputs

- N004 / R004.

## Prerequisites

- Agent subbundle architecture checkpoint after SB03.

## Exact Source References

- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\Api\WorkflowsApi.cs`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Models\Workflows\WorkflowCatalogModels.cs`
- `C:\repositories\CanDoItAll\src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Abstractions\WorkflowServiceContracts.cs`
- `C:\repositories\CanDoItAll\src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Core\WorkflowCatalogServices.cs`
- `C:\repositories\CanDoItAll\src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Core\InMemoryWorkflowCatalogStore.cs`
- `C:\repositories\CanDoItAll\tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs`
- `C:\repositories\CanDoItAll\tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs`

## Deliverables

- Public provenance fields and stable lookup/filter routes.
- Workspace-local uniqueness/ambiguity/staleness policy.
- Targeted catalog/API tests and typed response metadata.

## Dependency Impact

- Provides stable workflow/version identity used by SB05 idempotency fingerprints.

## Validation Depth

- Proof tier: `Behavioral`.
- Not critical alone; SB05 depends on its exact identity semantics.

## Implementation Steps

1. Trace template materialization provenance through persisted catalog models.
2. Add lower-level query contract and explicit resolution result.
3. Map stable lookup/filter routes and response DTO.
4. Test exact, missing, ambiguous/stale, and version-pinning cases.

## Scope Exceptions

- Partner external-key workflow provisioning is added only if current catalog save/import
  can persist it without a separate migration program.

## Do Not Do

- Do not use display name as identity or auto-select ambiguous materializations.

## Acceptance Checklist

- [x] template key is visible in catalog/detail
- [x] source pack/version/hash visible
- [x] exact lookup returns workflow and version ids
- [x] ambiguous/stale state is explicit
- [x] workspace visibility enforced

## Proof Required

- Catalog unit/integration tests and Workflow API integration tests.
- Affected build and OpenAPI route/DTO assertions.

## Browser Validation Logging

- N/A.

## C# Architecture Impact

### Boundary Ownership

- Workflows Abstractions/Core own lookup; Web only maps transport.

### Dependency Direction

- No Web types enter workflow core.

### Pattern Decision

- Explicit query/result contract; no additional pattern.

### Testability Contract

- Catalog lookup tested directly against deterministic store state.

### Partial Class Policy

- N/A; no new partial.

### Architecture Proof Required

- Source/dependency proof that materialization provenance remains in workflow owner.

## Progression Gate

- Lookup/pinning negative and positive tests pass, unlocking SB05.

## Reopen Triggers

- SB05 cannot fingerprint an unambiguous version or OpenAPI hides provenance.

## Closure Evidence

- Stable identity policy/query is owned by Workflows Core and resolves through the
  active `IWorkflowCatalogService`; Web owns only the focused transport mapper.
- In-memory and persistent catalogs retain template pack/version/hash, template key, and
  normalized partner external identity across versions, status changes, and preserved-id
  imports.
- PostgreSQL migration `20260726022532_AddWorkflowStableExternalIdentity` adds
  workspace-head external identity columns and a filtered unique index; EF reports no
  pending model changes.
- Unit proof: 8/8 passed for exact, missing, ambiguous, stale, normalized external,
  duplicate, provenance, and runnable-version behavior.
- Integration proof: 4/4 passed against the canonical persistent catalog for HTTP routes,
  external filtering, OpenAPI schemas, authorization, and two-host workspace isolation.
- Final scoped CodeAnalytics snapshot `snap-20260726023746-5a6a0c3e` has no blocking
  errors and only the exact two pre-existing AgentFramework module/type cycles from the
  preparation snapshot.
