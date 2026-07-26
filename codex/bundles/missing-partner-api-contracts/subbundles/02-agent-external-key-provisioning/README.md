# Agent External-Key Provisioning

## Status

- `Completed`

## Objective

- Close N002 with atomic workspace-scoped lookup, upsert, and guarded archive/delete by
  partner-owned namespace/key.

## Success Criteria

- Parallel identical requests with one idempotency key produce one agent and one binding.
- Reused key with different payload or stale version returns 409 without mutation.
- Read and archive/delete operations enforce the same workspace boundary.

## Covered Inputs

- N002 / R002 and the external-key portion shared with N001.

## Prerequisites

- SB01 closed and package import external-identity semantics remain compatible.

## Exact Source References

- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\Api\AgentsApi.cs`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Models\Editors\EditorModels.cs`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Core\Catalog\AgentFrameworkWorkspaceCatalogService.Agents.cs`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Models`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Core`
- `C:\repositories\CanDoItAll\tests\Unit\CanDoItAll.Tests.Unit`
- `C:\repositories\CanDoItAll\tests\Integration\CanDoItAll.Tests.Integration`

## Deliverables

- Normalized external identity and opaque configuration-version contracts.
- Durable request fingerprint/idempotency claim.
- GET, PUT, and guarded archive/delete routes with typed responses and conflicts.
- Direct policy/service tests plus parallel HTTP integration tests.

## Dependency Impact

- Critical identity foundation for agent recruiting and final public DTO/OpenAPI work.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical foundation.

## Implementation Steps

1. Define normalization, uniqueness, version, and canonical payload hash.
2. Persist binding and idempotency result in the owning workspace transaction.
3. Add focused provisioning service and thin workspace compatibility facade if required.
4. Map typed HTTP routes and conflict responses.
5. Run parallel identical/conflicting/stale/visibility tests.

## Scope Exceptions

- No new global tenant system; current workspace/profile is the scope.

## Do Not Do

- Do not use name or template key as partner identity.
- Do not use an in-memory cache as the source of idempotency.
- Do not silently merge stale configuration.

## Acceptance Checklist

- [x] one binding/agent after concurrent identical requests
- [x] opaque version changes after configuration mutation
- [x] stale `If-Match` fails
- [x] idempotency-key payload mismatch fails
- [x] cross-workspace key cannot be read or mutated

## Proof Required

- Isolated normalization/fingerprint/concurrency tests.
- Parallel API integration tests and affected builds.
- Semantic negative that a name-only duplicate check would fail.

## Closure Evidence

- Focused Core build passed with 0 warnings and 0 errors.
- Full Web composition build passed with 0 errors; only the recorded baseline NU1903
  package-advisory warnings remained.
- `AgentExternalProvisioningServiceTests`: 2/2 passed, covering canonical replay and
  exact no-mutation on stale `If-Match`.
- `AgentExternalProvisioningApiIntegrationTests`: 2/2 passed outside the filesystem
  sandbox required by the existing test-host secret vault. The suite proves concurrent
  identical PUTs create one durable agent/binding, changed replay conflicts, GET and
  ETag parity, stale archive rejection, guarded archive, real file-store cardinality,
  and inherited authorization.
- SB01 dependent-flow regression: package import now writes the same normalized binding;
  its 9 unit tests passed and its create/read/binding HTTP case passed. A separate auth
  case passed on retry after one transient PostgreSQL lease-cleanup permission failure.
- Scoped CodeAnalytics snapshot `snap-20260726002038-b2a91453`: 1,898 dependency edges,
  zero cycles, no blocking errors, and no `.csproj` changes.
- Source assertion: Web only binds routes/headers; `AgentExternalProvisioningService`
  owns normalization, fingerprints, preconditions, atomic mutations, and the durable
  command ledger; existing workspace/current-profile surfaces delegate.

## Scope Boundary Evidence

- Bindings live inside the current profile/workspace catalog. A different profile cannot
  observe or mutate them; this repository has no cross-workspace global identity registry,
  matching the declared “no new global tenant system” exception.
- Names are never queried for identity. The route resolves only normalized namespace/key
  bindings; existing template-key uniqueness remains a separate catalog invariant.

## Closure Decision

- Behavioral proof tier: `Pass`.
- Critical architecture foundation: `Pass`; dependent package-import flow revalidated.
- N002: `Solved`.
- Downstream progression: SB03 may enter validation.

## Browser Validation Logging

- N/A.

## C# Architecture Impact

### Boundary Ownership

- Core catalog provisioning service owns identity/idempotency; Web owns headers/routes.

### Dependency Direction

- Models/Core remain below Web and Persistence.

### Pattern Decision

- Durable Command/Ledger.

### Testability Contract

- Provisioning service is directly tested with a deterministic workspace store.

### Partial Class Policy

- Existing catalog partial may delegate but cannot own new policy.

### Architecture Proof Required

- Atomic store mutation and old-class delegation proof.

## Progression Gate

- Concurrent and stale/conflicting tests pass; architecture checkpoint unlocks SB03.

## Reopen Triggers

- Any duplicate, non-durable replay, or later package-import identity inconsistency.
