# SB02 entity, index, and foreign-key inventory

Workspace owns five explicit relational entities, each configured in a cohesive top-level file.

| Table / entity | Stable identity and indexes | Foreign keys and delete behavior | Concurrency / checks |
| --- | --- | --- | --- |
| `Workspace_ProviderSharePublications` / `ProviderSharePublication` | PK `Id`; alternate unique `PublicId`; alternate `(PublicId, ProviderProfileId)`; unique `ProviderProfileId`; lookup `(IsPublished, UpdatedAtUtc)` | `ProviderProfileId -> Workspace_ProviderProfiles.Id`, `Restrict` | application token; `PublicId <> ProviderProfileId` |
| `Workspace_SharedProviderSources` / `SharedProviderSource` | PK `Id`; `BaseUri`; status lookup `(IsEnabled, Status, UpdatedAtUtc)` | `ApiTokenSecretId -> Security_SecretRecords.Id`, `Restrict` | application token; one secret-record reference only |
| `Workspace_SharedProviderImports` / `SharedProviderImport` | PK `Id`; unique `(SourceId, RemotePublicationId)`; unique `ProviderProfileId`; state lookup `(SelectionState, AvailabilityState, UpdatedAtUtc)` | source and local profile FKs, both `Restrict` | application token; relational identity is not the JSON cache |
| `Workspace_SharedProviderServiceIdentity` / `SharedProviderServiceIdentity` | singleton PK `Id`; unique public source-instance ID | none | check pins the one allowed singleton row ID |
| `Workspace_SharedProviderInvocations` / `SharedProviderInvocationRecord` | PK `Id`; unique `RequestId`; publication/start and retention indexes | composite `(PublicationId, ProviderProfileId)` to publication alternate key, `Restrict` | application token; completion-state check |

The composite invocation FK prevents a valid publication public ID from being paired with a
different internal provider profile. All provider/source/secret relationships are restrictive;
the typed application deletion policy supplies actionable failures while PostgreSQL remains the
authoritative last line of defense.

