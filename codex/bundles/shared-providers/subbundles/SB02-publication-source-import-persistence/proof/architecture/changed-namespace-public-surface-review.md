# SB02 changed namespace, type, and public-surface review

## Namespace and dependency delta

| Namespace / project | SB02 change | Dependency result |
| --- | --- | --- |
| `CanDoItAll.Modules.Workspace` | entities, configurations, transitions, services, reconciliation, audit, deletion policy | adds only the authorized project/namespace dependency on `CanDoItAll.SharedProviders.Abstractions`; existing Infrastructure, Security, Models, and EF dependencies remain |
| `CanDoItAll.Infrastructure.Persistence` | named PostgreSQL uniqueness classifier on the existing serializable mutation scope | adds no package/project edge; Npgsql was already owned here |
| `CanDoItAll.AgentFramework.Usage` | shared-relay workload/consumer enum members and selection semantics | adds no dependency and preserves all existing numeric enum values and `Both` behavior |
| `CanDoItAll.Migrations.PostgreSql.Migrations` | generated migration/designer/snapshot | registry discovery only; Foundation/Migrations does not reference Workspace |

CodeAnalytics confirms 24-to-25 direct product references, exactly one new edge, and zero project
cycles. No SharedProviders.Http, Web, UI, or provider-SDK dependency enters Workspace.

## Every new Workspace public declaration

All 36 public declarations are explicit below. Public visibility is required for EF entity/model
discovery, downstream Web/Composition application consumers, typed production deletion failures,
or direct pure-policy testing. EF configurations, the state guard, and provider-specific conflict
classifier remain internal.

| Role | Public declarations reviewed | Decision |
| --- | --- | --- |
| Relational entities | `ProviderSharePublication`; `SharedProviderSource`; `SharedProviderImport`; `SharedProviderServiceIdentity`; `SharedProviderInvocationRecord` | keep public entity types; properties are strongly typed where contract identity exists and contain no wire DTO/content |
| State and snapshots | `SharedProviderSourceStatus`; `SharedProviderCatalogIdentityAcceptance`; `SharedProviderSelectionState`; `SharedProviderAvailabilityState`; `SharedProviderInvocationOutcome`; `SharedProviderMetadataCompleteness`; `SharedProviderInvocationCompletion`; `SharedProviderRemotePublicationSnapshot`; `SharedProviderRemotePublicationState` | keep public for deterministic application/state boundaries; remote snapshot is bounded, versioned, and sanitized |
| Pure transition APIs | `SharedProviderPublicationTransitions`; `SharedProviderSourceTransitions`; `SharedProviderImportTransitions`; `SharedProviderInvocationTransitions` | keep public static policy seams; no persistence/network/service locator side effect |
| Application services | `SharedProviderPublicationStore`; `SharedProviderSourceService`; `SharedProviderReconciliationCoordinator`; `SharedProviderServiceIdentityStore`; `SharedProviderInvocationAuditService` | keep public for scoped DI and downstream API/relay composition; each has one focused responsibility |
| Write/result/failure contracts | `SharedProviderSourceWriteRequest`; `SharedProviderSourceWriteResult`; `SharedProviderPublicationWriteResult`; `SharedProviderSourceFailureKind`; `SharedProviderSourceFailure`; `SharedProviderReconciliationRequest`; `SharedProviderReconciliationOutcome`; `SharedProviderReconciliationResult`; `SharedProviderInvocationStartRequest`; `SharedProviderConcurrencyException` | keep public strongly typed application contracts; none is exposed as an HTTP DTO and none accepts a secret value/content body |
| Deletion policy | `SharedProviderProfileReferenceKinds`; `SharedProviderProfileDeletionBlockedException`; `SharedProviderProfileDeletionPolicy` | keep public because both Workspace and AgentFramework deletion surfaces consume the same typed rule |

## Modified existing public surface

- `ProviderUsageWorkloadKind.SharedProviderRelay = 3` and
  `ProviderUsageConsumerKind.SharedProviderRelay = 3` append truthful classifications without
  renumbering existing values.
- `ProviderUsageWorkloadSelection.SharedProviderRelays = 4` and `All` extend the flags while
  preserving existing `Both = Agents | SimpleChats`; unknown legacy contributions remain visible
  for `Both` and `All`.
- `SerializableMutationScope.IsUniqueConstraintConflict(Exception, IReadOnlySet<string>)` is one
  narrow Foundation public helper. It prevents Workspace from importing Npgsql details and only
  recognizes SQLSTATE unique violations whose exact constraint name is allowlisted. Nulls fail
  explicitly and unrelated database errors pass through.

## Critical Foundation producer-to-consumer proof

`SerializableMutationScope.IsUniqueConstraintConflict` is produced in Infrastructure, wrapped by
the internal Workspace `SharedProviderPersistenceConflictClassifier`, and consumed by
`SharedProviderPublicationStore` and `SharedProviderReconciliationCoordinator`. The 14-test real
PostgreSQL lane proves concurrent publication identity convergence and duplicate import identity
rejection/translation. This is downstream behavioral proof of the critical Foundation helper,
not merely a source-reference assertion.

No new partial type, reflection bridge, dynamic dispatch, duplicate DTO, or service locator was
introduced. The largest handwritten SB02 production file is 217 lines.

