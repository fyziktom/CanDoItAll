# SB01 public type and member inventory

Namespace: `CanDoItAll.SharedProviders.Abstractions`. Every implementation-only JSON converter,
validation helper, Web state, and Web middleware remains internal.

| Public type(s) | Public surface | Boundary reason |
| --- | --- | --- |
| `SharedProviderProtocol`, `SharedProviderHeaders`, `SharedProviderRoutes` | schema/header/route constants; catalog/OpenAI-base URI resolvers | One repository-owned version and route vocabulary with base-path-safe joining. |
| `SharedProviderProtocolVersion` | constructor, `Value`, `Current`, `TryParse`, `ToString` | Strong exact `1.0` version; invalid default fails predictably. |
| `SharedProviderPurpose`, `SharedProviderTransport`, `SharedProviderCapability`, `SharedProviderHealthState` | frozen enum members with explicit wire converters | Strong, case-sensitive advertised contract; no magic strings at callers. |
| `SharedProviderProtocolDescriptor`, `SharedProviderCatalogModel`, `SharedProviderCatalogHealth`, `SharedProviderCatalogPublication`, `SharedProviderCatalogDocument` | immutable primary-constructor properties | Sanitized catalog wire records only; no profile, secret, URI, raw error, or content field. |
| `SharedProviderProtocolJson` | read-only `Options`; `SerializeCatalog`, `DeserializeCatalog`, `ValidateCatalog` | Single strict serializer/validator and defensive canonical copy boundary. |
| `SharedProviderCanonicalRevision` | `ComputeCatalog`, `ComputePublication` | Single strong SHA-256 canonical-revision implementation. |
| `SharedProviderPublicationId`, `SharedProviderSourceInstanceId` | guarded constructor, `Value`, `New`, `TryParse`, `ToString` | Stable public identities that cannot be empty or confused with internal profile IDs. |
| `SharedProviderPublicRevision` | `Prefix`, `HashLength`, guarded constructor, `Value`, `TryParse`, `ToString` | Strong lowercase SHA-256 revision/ETag source. |
| `SharedProviderRoutingModelId` | `Value`, `ToString` | Opaque URL/JSON-safe routing value; only the codec can construct/parse it. |
| `SharedProviderRoutingModelRoute` | validated `PublicationId`, `ModelFingerprint` | Server-side resolved public route without model text or private target data. |
| `SharedProviderRoutingModelIdCodec` | `VersionPrefix`, model length bound, `Create`, `Parse`, `TryParse`, `Matches` | One deterministic collision-resistant routing codec and resolver match rule. |
| `AccessContextReference` | length bound, guarded constructor, `Value`, `Parse`, `TryParse`, `ToString` | Bounded opaque correlation metadata; invalid default cannot leak as an empty value. |
| `IAccessContextReferenceAccessor` | nullable read-only `Current` | Minimal request-scoped read seam without ASP.NET Core dependency. |
| `SharedProviderFailureCategory`, `SharedProviderFailureCode`, `SharedProviderFailure` | frozen categories; bounded code/message/parameter/retry fields | SDK-neutral, sanitized, explicitly bounded failure contract. |
| `SharedProviderCatalogEntityTag`, `SharedProviderCatalogFetchRequest` | guarded ETag and absolute base-URI request; conditional ETag | SDK-neutral catalog client input with strong ETag semantics. |
| `SharedProviderCatalogFetchResult` plus `Succeeded`, `NotModified`, `Failed` | validated discriminated result records | Exhaustive catalog outcomes; success requires catalog revision/ETag agreement. |
| `ISharedProviderCatalogClient` | `FetchAsync` | Outward catalog port; no HTTP implementation detail. |
| `SharedProviderRelayOperation`, `SharedProviderStreamingMode` | frozen supported operation/mode members | Strong dispatch vocabulary for downstream adapters. |
| `SharedProviderRelaySupportDescriptor` | capability flags, operation set, and request/output/image bounds | Immutable coherent adapter capability intersection; recursively copied set. |
| `SharedProviderInferenceTransportRequest` | absolute source base URI, operation, routing ID, bounded payload, stream flag | SDK-neutral inference port input; rejects invalid route/operation/size combinations. |
| `SharedProviderInferenceTransportResult` plus `Buffered`, `Streaming`, `Failed` | validated buffered JSON, async byte chunks, or typed failure | Exhaustive transport outcomes without provider SDK or Web types. |
| `ISharedProviderInferenceTransport` | `InvokeAsync` | Outward inference port; concrete HTTP belongs to SB04. |

## Review result

All public types are cohesive boundary contracts. No public Web type was added. No public type
references `HttpContext`, EF, Workspace, Web, MAF, a provider SDK, an authentication principal,
a secret record, or an internal provider profile. Nested result variants are intentional closed
outcomes, not a nested implementation architecture. No partial class was created or extended.
