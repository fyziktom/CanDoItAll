# BR03 — Relocate the shared-provider control plane

## Objective

Move the complete shared-provider domain, persistence, and application layer from Workspace to ProviderManagement without changing protocol or behavior.

## Source scope

Relocate all production code currently under:

`src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/**`

This includes, as applicable:

- publication and eligibility
- service identity
- remote source configuration and synchronization
- discovery client orchestration
- imported-provider state and reconciliation
- runtime materialization
- relay application service and dispatcher
- authentication/authorization integration
- rate limiting
- invocation audit and recovery
- image target resolution
- health checks and hosted recovery services
- connector manifest contribution
- EF entities and configurations

## Implementation rules

1. Move ownership and namespace to ProviderManagement.
2. Preserve behavior, IDs, route DTOs, revisions, state-machine transitions, error semantics, logs, and cancellation behavior.
3. Replace dependencies on Workspace provider services with ProviderManagement-native services introduced in BR02.
4. Keep `CanDoItAll.SharedProviders.Abstractions` as the protocol boundary.
5. Keep outbound HTTP transport in its existing transport project where appropriate.
6. Do not rewrite working algorithms merely to fit new names.
7. Remove the old Workspace directory rather than leaving forwarding copies.
8. Keep Web endpoint route mapping in Web; rewire service dependencies either now or in BR05 while maintaining a buildable checkpoint.
9. Hosted-service registration moves to ProviderManagement/Composition. Workspace DI must not register it.

## Persistence controls

Retain exact physical mappings for:

- provider share publications
- service identity
- remote sources
- invocations
- imports

No table rename, drop, recreate, or data copy is allowed.

## Focused tests

Proof tier: Behavioral

Selected owning tests:

- `SharedProviderPublicationAndCatalogTests`
- `SharedProviderStateModelTests`
- `SharedProviderReconciliationTests`
- `SharedProviderRuntimeProfileMaterializerTests`
- `SharedProviderRelayPolicyTests`
- `ProviderCatalogProjectionFailureTests`
- `ProviderManagementBoundaryTests`
- `SharedProviderArchitectureCharacterizationTests`
- `SharedProviderSourceSyncIntegrationTests`
- `SharedProviderDeletionReferenceIntegrationTests`
- `SharedProviderAuthorizationIntegrationTests`
- `SharedProviderCatalogApiIntegrationTests`
- `SharedProviderOpenAiCompatibilityIntegrationTests`
- `SharedProviderBackendCheckpointIntegrationTests`
- `SharedProviderRuntimeProjectionIntegrationTests`
- `SharedProviderRuntimePathCharacterizationTests`

`SharedProviderPersistenceIntegrationTests` is not executed because every test
in the class provisions PostgreSQL through `docker compose`, while Docker is
explicitly denied for this bundle. Physical mapping preservation is verified by
source scans, migration-diff checks, and the non-container state-model tests.

The exact filters and discovery counts are frozen after the owning sources are
updated and before the focused runs. The impacted-test analyzer is rerun after
implementation; healthy high-confidence additions are included before freeze.
The broad non-container suites remain deferred to BR07.

Frozen unit filter (121 discovered tests):

`FullyQualifiedName~SharedProviderPublicationAndCatalogTests|FullyQualifiedName~SharedProviderStateModelTests|FullyQualifiedName~SharedProviderReconciliationTests|FullyQualifiedName~SharedProviderRuntimeProfileMaterializerTests|FullyQualifiedName~SharedProviderRelayPolicyTests|FullyQualifiedName~ProviderCatalogProjectionFailureTests|FullyQualifiedName~ProviderManagementBoundaryTests|FullyQualifiedName~SharedProviderArchitectureCharacterizationTests`

Frozen integration filter (100 discovered tests):

`FullyQualifiedName~SharedProviderSourceSyncIntegrationTests|FullyQualifiedName~SharedProviderDeletionReferenceIntegrationTests|FullyQualifiedName~SharedProviderAuthorizationIntegrationTests|FullyQualifiedName~SharedProviderCatalogApiIntegrationTests|FullyQualifiedName~SharedProviderOpenAiCompatibilityIntegrationTests|FullyQualifiedName~SharedProviderBackendCheckpointIntegrationTests|FullyQualifiedName~SharedProviderRuntimeProjectionIntegrationTests|FullyQualifiedName~SharedProviderRuntimePathCharacterizationTests`

Move or update tests for:

- publication eligibility and catalog projection
- source synchronization
- import reconciliation and deletion policies
- shared effective profile materialization
- missing/invalid secret fail-closed behavior
- relay authorization
- rate limiting
- audit/recovery
- image target routing

Tests must assert behavior, not the old Workspace namespace.

## Acceptance

- `Modules.Workspace/SharedProviders` is absent.
- Workspace DI contains no shared-provider registration.
- ProviderManagement has no Workspace dependency.
- Shared-provider APIs and focused tests retain behavior.
- Affected projects build without type forwarding through Workspace.

## Commit

`BR03: relocate shared provider control plane`
