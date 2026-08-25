# Target domain and data model

## Terminology

- **Local provider profile:** an ordinary Workspace `ProviderProfile`.
- **Publication:** an explicit central share boundary over one local profile.
- **Source:** a client-side central CanDoItAll or future EGCP endpoint.
- **Remote publication:** one sanitized provider entry returned by a source.
- **Import:** the durable client relationship between a source publication and a stable local
  provider profile.
- **Routing model ID:** the public OpenAI-compatible `model` value that resolves to one
  publication and upstream model.
- **Local enabled intent:** whether the client user wants the imported profile available.
- **Remote availability:** whether the source still advertises and accepts the publication.

## Proposed Workspace entities

Names may be adjusted to current repository conventions in SB00, but semantics are fixed.

### `ProviderSharePublication`

| Field | Purpose |
| --- | --- |
| `Id` | internal row identity |
| `ProviderProfileId` | unique FK to central `ProviderProfile` |
| `PublicId` | stable public UUID, never equal to provider profile ID |
| `IsPublished` | explicit share state |
| `CreatedAtUtc`, `UpdatedAtUtc` | administration metadata |
| `ConcurrencyToken` | application-managed optimistic concurrency |

Constraints:

- unique `ProviderProfileId`;
- unique `PublicId`;
- publication row may remain when unpublished so public identity can be reused;
- deletion of a provider with publication/import references follows existing provider-reference
  policy and must not orphan rows.

### `SharedProviderSource`

| Field | Purpose |
| --- | --- |
| `Id` | local source identity |
| `Name` | local display name |
| `BaseUri` | canonical source/EGCP base URI |
| `ApiTokenSecretId` | one existing secret reference |
| `IsEnabled` | local source intent |
| `AllowInsecurePrivateNetwork` or policy key | explicit network policy selection |
| `RemoteInstanceId` | last trusted remote stable identity |
| `LastCatalogETag` | conditional sync token |
| `LastSyncAtUtc` | status |
| `LastStatusCode`, `LastStatusMessage` | sanitized status |
| `ConcurrencyToken` | optimistic concurrency |

Do not store a raw API token, Authorization header, URI userinfo, query, or fragment.

### `SharedProviderImport`

| Field | Purpose |
| --- | --- |
| `Id` | local import identity |
| `SourceId` | FK to source |
| `RemotePublicationId` | stable source public ID |
| `ProviderProfileId` | unique FK to local profile |
| `RemoteDisplayName` | current source-owned name |
| `RemoteRevision` | canonical public representation revision |
| `RemotePurpose` | source-owned purpose |
| `RemoteTransport` | source-owned protocol/transport |
| `RemoteDefaultModelId` | public routing model |
| `RemoteCatalogSnapshotJson` | bounded versioned sanitized snapshot/cache |
| `SelectionState` | selected/retired |
| `AvailabilityState` | available/unpublished/missing/source-offline/auth-failed/identity-mismatch/incompatible |
| `LastSeenAtUtc`, `LastSyncAtUtc` | reconciliation metadata |
| `ConcurrencyToken` | optimistic concurrency |

Constraints:

- unique `(SourceId, RemotePublicationId)`;
- unique `ProviderProfileId`;
- local profile ID is preserved across sync/unpublish/reappearance;
- catalog snapshot JSON is a cache of a validated public contract, not the relational identity.

### `SharedProviderInvocationRecord`

Metadata-only durable record:

- invocation/request ID;
- central publication public ID and internal provider profile ID;
- authenticated token subject;
- optional access-context reference;
- trace/correlation identifiers;
- operation and public/upstream model;
- start/end timestamps and latency;
- status/outcome/error category;
- prompt/completion token counts or generated-image count, usage completeness, pricing
  completeness, and cost when available;
- no request/response content;
- retention/cleanup timestamp or policy metadata.

It is the canonical relay execution observation and may feed the existing provider usage
projection. Do not create a second cost total table.

### Stable source service identity

Catalog needs a stable remote instance ID. SB00 must search for a canonical existing
installation/host identity. Reuse it only if it remains stable across restart and is appropriate
to expose.

If no suitable identity exists, add a one-row Workspace-owned
`SharedProviderServiceIdentity` with a generated public UUID. Do not use a transient process ID,
container ID, database connection string, host binding environment variable, or Workspace
provider profile ID.

## Imported local provider profile

The linked local `ProviderProfile` uses:

- `ConnectorPluginKey = "provider.candoitall-shared"`;
- local `Name` as editable alias;
- local `IsEnabled` as user intent;
- effective central inference base URI and source secret reference materialized from the
  canonical source;
- public routing model ID as `DefaultModel`;
- runtime/provider metadata projected from the validated import snapshot;
- capability flags derived from catalog, not freely editable;
- no upstream central provider endpoint or upstream secret.

SB02/SB06 must choose one consistent materialization mechanism:

1. join source/import during effective-profile construction; or
2. transactionally maintain derived `BaseUrl`/secret-reference caches in the linked profile.

The first is cleaner if it does not force async/database work into inner mappers. The second is
acceptable only with invariant tests proving source edits update every import atomically.
Secret values are never copied in either design.

## State transitions

```text
Discovered -> Selected/Available -> TemporarilyUnavailable -> Available
                         |                  |
                         v                  v
                      Retired       Unpublished/Missing
                                            |
                                            v
                                         Available
```

Additional terminal/blocking states:

- `SourceIdentityMismatch`
- `IncompatibleContract`
- `AuthorizationFailed`

A transient sync failure updates source status but does not mark every import missing or
delete it. Absence is concluded only from a successful authoritative catalog response.

## SB02 realized model — 2026-08-24

The proposed five-row model is implemented under Workspace with the specified unique identities,
application-managed concurrency, restrictive relationships, retention lookup, and completion/
identity checks. `SharedProviderServiceIdentity` is the selected one-row stable installation
identity because no suitable existing public installation identifier was found.

SB02 selected the derived-cache materialization option: `SharedProviderSource` owns the canonical
URI and one existing secret-record ID; every linked profile caches the resolved shared OpenAI base
and the same reference ID. A real two-import transaction test proves both caches change together
while both local aliases/enabled intents remain unchanged. The stale-token test proves rejected
state does not persist.

Remote catalog cache JSON is limited to a versioned 256-KiB sanitized contract envelope and owns
no identity. Generic imported-profile editing remains fail-closed until SB06/SB08 add the runtime
connector and server-side ownership policy in that order.

## SB04 realized invocation usage — 2026-08-25

Relay usage is operation-disjoint and never invents zero:

- unavailable usage has null prompt tokens, completion tokens, and image count;
- partial token usage has exactly one token count and no image count;
- complete Chat/Responses usage has both token counts and no image count;
- complete Image Generations usage has only a positive image count;
- image count is bounded to 1–16 in persistence, while the selected adapter descriptor may impose
  a narrower request maximum.

`SharedProviderInvocationRecord.ImageCount` is nullable. The Workspace EF configuration,
PostgreSQL migration, migration designer, and model snapshot carry the same operation-aware check
constraint. Projection maps complete image observations into the existing usage pipeline and
fails explicitly for inconsistent stored rows; aggregation rejects non-positive and mixed
token/image observations. Additive init-only `ImageCount` properties preserve the existing
constructor and deconstruction ABI on invocation completion, usage contribution, and usage
totals contracts.
