# CanDoItAll.SharedProviders.Http

Implements bounded catalog retrieval, source URI/address policy, request normalization, provider relay adapters, response rewriting, SSE parsing, and usage extraction. It consumes the shared contracts and provider runtime ports; Web owns public HTTP status and stream termination.

Use the repository-pinned .NET SDK and the sibling source dependencies described in the [root README](../../../README.md). Run these commands from the repository root:

```powershell
dotnet build ./src/Integration/CanDoItAll.SharedProviders.Http/CanDoItAll.SharedProviders.Http.csproj --configuration Release /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Integration.slnx --configuration Release --filter "FullyQualifiedName~SharedProviderStreamingIntegrationTests" /m:1
```

Persistence cases create disposable databases on the configured test PostgreSQL instance. Fake upstream tests do not require paid provider credentials. See the test guide before enabling any Docker or live-host lane.

See [shared providers](../../../docs/shared-providers.md), [request history](../../../docs/provider-request-history.md), [architecture](../../../docs/architecture/overview.md), and [testing](../../../docs/testing.md).
