# CanDoItAll.Modules.AgentFramework.ProviderManagement

Owns provider administration and its Blazor surfaces, publication/source/import lifecycle, catalog projection and routing, invocation audit, and history source integration. Protocol transport belongs to SharedProviders.Http; provider execution belongs to the provider/MAF runtime; Web and Composition own endpoint registration and wiring.

Runtime persistence uses `ProvidersDbContext`, explicitly mapping profiles, publications, sources,
imports, invocation audit and service identity. The pooled factory uses the immutable canonical
database profile. Existing tables, identifiers, GUID concurrency tokens, indexes and source-to-secret
foreign key remain unchanged in the complete migration model; the runtime model does not map Security
entities. The service identity retains its existing non-token mapping.

Security supplies bulk secret-reference existence facts for catalog and execution reads, and explicit
enlisted reads for mutations. Secret deletion policies read Provider records in the same actual
serializable transaction. Catalog reads remain bounded by a fixed number of queries, with cache stamps
including observed secret existence. Normal owner factories stay independent of transaction scopes.
Commit observers and catalog refresh run after the coordination scope is released; existing saved-state
warnings, source trust, concurrency conflicts and immutable invocation/history behavior are retained.

Technical-agent catalog projection, CRM enrichment, bootstrap and complete database transfer still
have separate integration boundaries. This context extraction does not establish their final ownership
or replay guarantees. Complete schema migrations remain outside ordinary runtime business access.

Use the repository-pinned .NET SDK and the sibling source dependencies described in the [root README](../../../README.md). Run these commands from the repository root:

```powershell
dotnet build ./src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/CanDoItAll.Modules.AgentFramework.ProviderManagement.csproj --configuration Release /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Unit.slnx --configuration Release --filter "FullyQualifiedName~SharedProviderPublicationAndCatalogTests" /m:1
```

See [shared providers](../../../docs/shared-providers.md), [request history](../../../docs/provider-request-history.md), [architecture](../../../docs/architecture/overview.md), and [testing](../../../docs/testing.md).
