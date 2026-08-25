# SB03 changed namespace, public surface, and partial-class review

State: `PASS`.

## Namespace and dependency decisions

| Owner | SB03 responsibility | Dependency decision |
| --- | --- | --- |
| `CanDoItAll.SharedProviders.Abstractions` | SDK-free relay-support descriptor and catalog port; reuses the SB01 public DTO/revision/routing contracts | remains inward with no product dependency |
| `CanDoItAll.SharedProviders.Http` | immutable production support rows only; no dispatch in SB03 | references only Abstractions; registered by outer Composition |
| `CanDoItAll.Modules.Workspace` | canonical metadata, eligibility, publication application, sanitized projection, routing index/query/cache, secret-reference policy | references Abstractions, never Http or Web |
| `CanDoItAll.Modules.Security` | generic typed secret-deletion reference contract and mutation key | does not reference Workspace; Workspace supplies an implementation |
| `CanDoItAll.Modules.AgentFramework` | actual registry-save producer for canonical Workspace rows | uses the existing allowed AgentFramework-to-Workspace application dependency; no wire/Http dependency |
| `CanDoItAll.Web` | catalog/models routing, policy metadata, error and OpenAPI adapters | delegates catalog state to the Workspace query port; no upstream/provider dispatch |
| `CanDoItAll.Composition` | concrete Http descriptor registration and managed-provider canonical metadata | outermost authorized owner of implementation selection |

## New or expanded public surface

Every SB03 public declaration was inspected. None is an HTTP serialization shortcut for EF or
AgentFramework provider profiles.

| Role | Public declarations | Review decision |
| --- | --- | --- |
| Relay registry contract | `SharedProviderRelayAdapterClassification`, `SharedProviderRelayAdapterDescriptor`, `ISharedProviderRelaySupportCatalog` | required cross-project Abstractions boundary; validates connector key, purpose, classification, and support payload |
| Relay registry implementation | `SharedProviderRelaySupportCatalog`, `SharedProviderHttpServiceCollectionExtensions` | required for outer composition and real-host registry proof; connector constants remain internal |
| Canonical metadata writer | `SharedProviderProfilePublicationMetadataWriter` | required by Workspace, AgentFramework, and Composition save/bootstrap producers; reader/schema/model validator remain internal |
| Eligibility application boundary | `SharedProviderPublicationEligibilityCode`, `SharedProviderEligibleModel`, `SharedProviderPublicationEligibility`, `SharedProviderPublicationEligibilityException`, `SharedProviderPublicationEligibilityPolicy` | required for SB08 presentation/application use and pure policy tests; contains sanitized reasons, not secrets/configuration |
| Public projection and future routing seam | `SharedProviderCatalogProjectionSource`, `SharedProviderRoutingTarget`, `SharedProviderCatalogSnapshot`, `SharedProviderCatalogProjection`, `SharedProviderPublicHealthMapper`, `SharedProviderCatalogProjector` | explicit testable policy/projection boundary; only snapshot DTO crosses to Web, while routing target is reserved for SB04 in-process dispatch |
| Cache/observer seam | `ISharedProviderPublicationCommitObserver`, `SharedProviderCatalogCache` | explicit post-commit invalidation/test seam; concrete profile/publication observers remain internal |
| Query ports | `ISharedProviderCatalogQueryService`, `ISharedProviderRoutingResolver`, `SharedProviderCatalogQueryService` | real Workspace-to-Web and Workspace-to-SB04 boundary; no EF type appears in either interface |
| Publication mutation | `SharedProviderPublicationAction`, `SharedProviderPublicationChangeRequest`, `SharedProviderPublicationApplicationService` | explicit SB08 application boundary with strong concurrency input |
| Secret mutation/reference | `SecretMutationScopeKeys`, `SecretDeletionReference`, `ISecretDeletionReferencePolicy`, `SecretDeletionBlockedException`, `ProviderProfileSecretMutationScope`, `WorkspaceProviderSecretDeletionReferencePolicy` | Security-to-Workspace extension boundary and cross-project AgentFramework save coordination; exception message is sanitized |
| Shared constants | new members on `ProviderProfileMetadataPropertyNames`; `ProviderProfileWellKnownIds`; new members on `ApiAccessScopeNames` | removes divergent parsing/IDs/scope literals while preserving existing values |
| Infrastructure overload | multi-key `SerializableMutationScope.BeginAsync` | required for stable old/target secret locking; preserves the existing single-key overload |

The pure projection/cache types currently remain public because the repository has no Workspace
`InternalsVisibleTo` test boundary and SB04 needs the routing seam. If later work consumes only the
two interfaces and routing target, the projector input/cache implementation types are candidates
for narrowing; that is not a reason to duplicate them or add a test-only production branch.

## Modified existing surface

- `SecretService` now requires `IEnumerable<ISecretDeletionReferencePolicy>`. The enumerable is
  materialized defensively and cannot be null; DI naturally supplies an empty set outside composed
  Workspace hosts.
- `WorkspaceService.SaveProviderAsync` and
  `WorkspaceBackedAgentProviderProfileRegistry.SaveProviderAsync` preserve their existing public
  signatures while adding canonical metadata and secret-existence behavior.
- Web endpoint and OpenAPI helpers are internal. Existing route-builder/service-collection public
  extensions gain one map/registration call rather than new stateful public controllers.

## Partial-class and cohesion review

No new partial type was introduced. Catalog, eligibility, application, query, cache, metadata,
secret mutation, Http registry, Web endpoint, error, and OpenAPI behavior live in cohesive
top-level files.

The existing `WorkspaceService` partial in `WorkspaceModels.cs` did grow at the real provider-save
and delete owners. This is a narrow documented exception to NFR-020: the transaction must validate
manifest/configuration/pricing, acquire the old/target secret scope, write the canonical row, save,
commit, and notify in that order. Reusable parsing/writing, eligibility, catalog, cache, and
deletion-policy logic were not placed in the monolith. The two private classification helpers are
bounded to existing save behavior; if a later connector adds another branch, extract a typed
classifier before extending the switch.

`ApiEndpointRouteBuilderExtensions` receives only the required thin map call. No catalog logic,
large endpoint partial, reflection bridge, service locator, duplicate wire DTO, or provider SDK
dependency was added.

The source review passes. The force-refreshed after snapshot
`snap-20260825012213-a17e36ed` reports 14 scoped projects, 33 direct product references, no
project-level cycle, the unchanged two module cycles and one type cycle, and no error finding.
That confirms the expected new Http project and two authorized product edges without a reverse
Workspace-to-Http or Abstractions-to-product dependency.
