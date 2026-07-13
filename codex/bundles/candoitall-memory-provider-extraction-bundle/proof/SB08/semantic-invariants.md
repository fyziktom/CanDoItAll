# SB08 Semantic Invariants

## Invariant SB08_MCP001

- Invariant ID: `SB08_MCP001`
- Source raw note: MCP-style providers must receive generic memory protocol requests through MCP abstraction tools without native or concrete MCP runtime coupling.
- Expected behavior: `McpMemoryProviderDriver.ExecuteContextQueryAsync` builds a profile-derived `McpServerDescriptor`, starts an `IMcpRuntimeClient`, calls the configured context query tool, and sends query text plus operation id, correlation id, provider id, capability id, protocol version, and full `MemoryOperationEnvelope<MemoryContextQueryRequest>`.
- Disallowed shallow implementation: passing only a raw query string, directly referencing the concrete MCP runtime package, or omitting operation/correlation metadata.
- Failing-first test: `bundle://proof/SB08/transcripts/failing-first-mcp-driver-tests.txt`
- Passing test: `SB08_MCP001` in `bundle://proof/SB08/transcripts/passing-mcp-driver-tests.txt`
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderDriver.cs`, `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderDriver.Requests.cs`, and `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderConfiguration.cs`
- Production assertions: `bundle://proof/SB08/transcripts/source-audit-mcp-driver-assertions.txt`
- Red-team negative case: the fake MCP client captures tool arguments and fails if the plain query payload or structured envelope metadata is missing.
- Downstream dependency check: SB09 and SB15 can use a typed accepted/status path without depending on concrete MCP runtime packages.

## Invariant SB08_MCP002

- Invariant ID: `SB08_MCP002`
- Source raw note: unsupported MCP capabilities must fail predictably during dispatch.
- Expected behavior: `ExecuteIngestionAsync` returns `McpMemoryAdapterResultKind.UnsupportedCapability` when the provider profile lacks an ingestion tool mapping, and it does not create an MCP client.
- Disallowed shallow implementation: attempting a best-effort tool name, silently dropping ingestion, or returning empty success.
- Failing-first test: `bundle://proof/SB08/transcripts/failing-first-mcp-driver-tests.txt`
- Passing test: `SB08_MCP002` in `bundle://proof/SB08/transcripts/passing-mcp-driver-tests.txt`
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderContracts.cs` and `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderDriver.cs`
- Production assertions: `bundle://proof/SB08/transcripts/source-audit-mcp-driver-assertions.txt`
- Red-team negative case: missing ingestion mapping keeps `IMcpClientFactory.CreateAsync` at zero calls.
- Downstream dependency check: source ingestion adapters can rely on structured unsupported-capability results instead of guessing whether an MCP provider supports ingestion.

## Invariant SB08_MCP003

- Invariant ID: `SB08_MCP003`
- Source raw note: async MCP memory operations need a generic status adapter.
- Expected behavior: `GetOperationStatusAsync` calls the configured operation status MCP tool with a typed operation status request and maps a returned `MemoryOperationResult` into `McpMemoryAdapterResultKind.OperationResult`.
- Disallowed shallow implementation: polling provider-specific endpoints, representing status as anonymous strings, or forcing long-running tools to block synchronously.
- Failing-first test: `bundle://proof/SB08/transcripts/failing-first-mcp-driver-tests.txt`
- Passing test: `SB08_MCP003` in `bundle://proof/SB08/transcripts/passing-mcp-driver-tests.txt`
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderRequests.cs`, `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderContracts.cs`, and `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderDriver.Responses.cs`
- Production assertions: `bundle://proof/SB08/transcripts/source-audit-mcp-driver-assertions.txt`
- Red-team negative case: the fake MCP client captures the operation id used as status tool input and the result remains a typed `MemoryOperationResult`.
- Downstream dependency check: SB09 async workers can use an MCP status adapter shape without direct MCP transport knowledge.

## Invariant SB08_MCP004

- Invariant ID: `SB08_MCP004`
- Source raw note: MCP providers that expose event polling must return generic provider events.
- Expected behavior: `PollEventsAsync` calls the configured event polling tool and maps `McpMemoryProviderEventPollResponse` into `McpMemoryAdapterResultKind.ProviderEvents`.
- Disallowed shallow implementation: pushing provider events directly into agent/workflow execution, skipping dedupe-ready event ids, or returning untyped JSON.
- Failing-first test: `bundle://proof/SB08/transcripts/failing-first-mcp-driver-tests.txt`
- Passing test: `SB08_MCP004` in `bundle://proof/SB08/transcripts/passing-mcp-driver-tests.txt`
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderContracts.cs`, `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderRequests.cs`, and `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderDriver.Responses.cs`
- Production assertions: `bundle://proof/SB08/transcripts/source-audit-mcp-driver-assertions.txt`
- Red-team negative case: the returned event is asserted by typed `MemoryProviderEventId`, not by string payload matching.
- Downstream dependency check: SB09 inbox/outbox workers can poll MCP providers through a typed event adapter.

## Invariant SB08_MCP005

- Invariant ID: `SB08_MCP005`
- Source raw note: MCP providers are optional and selected through the same generic provider driver kind as other providers.
- Expected behavior: `McpMemoryProviderDriver.DriverKind` is `MemoryProviderDriverKind.Mcp`; `AddMcpMemoryProviderDriver` registers the driver and adapter explicitly; missing context query tool mapping returns `UnsupportedCapability` without dispatch.
- Disallowed shallow implementation: registering an MCP fallback by default, adding special runtime selection logic, or invoking MCP with inferred tool names.
- Failing-first test: `bundle://proof/SB08/transcripts/failing-first-mcp-driver-tests.txt`
- Passing test: `SB08_MCP005` in `bundle://proof/SB08/transcripts/passing-mcp-driver-tests.txt`
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryServiceCollectionExtensions.cs`, `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderDriver.cs`, and `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryMcpDriverTests.cs`
- Production assertions: `bundle://proof/SB08/transcripts/source-audit-mcp-driver-boundary.txt`
- Red-team negative case: no context query tool mapping produces no MCP client creation and no hidden fallback provider.
- Downstream dependency check: SB10 runtime checkpoint can verify MCP is an opt-in driver path under the existing registry model.

## Invariant SB08_MCP006

- Invariant ID: `SB08_MCP006`
- Source raw note: provider manifests must declare effective MCP-backed memory capabilities.
- Expected behavior: `McpMemoryProviderManifestFactory.CreateManifest` maps configured MCP tools to supported Memory Protocol capability descriptors with version `mcp-tool.v1` and correct interaction support flags.
- Disallowed shallow implementation: storing MCP tool names only in opaque JSON, claiming unsupported capabilities, or omitting effective capability versions.
- Failing-first test: `bundle://proof/SB08/transcripts/failing-first-mcp-driver-tests.txt`
- Passing test: `SB08_MCP006` in `bundle://proof/SB08/transcripts/passing-mcp-driver-tests.txt`
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Abstractions/MemoryCapabilityIds.cs`, `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderManifestFactory.cs`, and `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderConfiguration.cs`
- Production assertions: `bundle://proof/SB08/transcripts/source-audit-mcp-driver-assertions.txt`
- Red-team negative case: an unconfigured ingestion tool does not produce an `ingestion.snapshot` capability descriptor.
- Downstream dependency check: provider management and assignment flows can use typed capability descriptors for MCP providers without inspecting MCP-specific JSON.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| MCP memory project | `repo://src/Memory/CanDoItAll.Memory.Mcp/CanDoItAll.Memory.Mcp.csproj` | `bundle://proof/SB08/transcripts/passing-solution-build.txt` | solution membership and opt-in DI registration | dependency boundary audit rejects native, Qdrant, OpenAI, RAG, EF, infrastructure, and concrete MCP runtime references |
| Profile-to-descriptor mapping | `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderConfiguration.cs` | `SB08_MCP001` | provider profile extensions create MCP abstraction descriptors and tool maps | missing required profile extensions throw explicitly |
| Context query driver | `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderDriver.cs` and `.Requests.cs` | `SB08_MCP001` | uses `IMcpClientFactory`, starts/stops runtime client, calls configured tool | no configured query tool returns unsupported without dispatch |
| Adapter contracts | `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderContracts.cs` | `SB08_MCP002`, `SB08_MCP003`, and `SB08_MCP004` | ingestion, status, and event polling expose typed generic results | unsupported ingestion and missing tools remain structured failures |
| Manifest mapping | `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderManifestFactory.cs` | `SB08_MCP006` | maps tool availability to Memory Protocol capabilities and interaction flags | missing tool mappings do not claim capabilities |
| Capability id constants | `repo://src/Memory/CanDoItAll.Memory.Abstractions/MemoryCapabilityIds.cs` | full memory suite in `bundle://proof/SB08/transcripts/passing-memory-test-suite.txt` | removes repeated magic strings for standard protocol capabilities | invalid capability ids still fail in existing protocol guard tests |
| Cohesion checkpoint compliance | `repo://src/Memory/CanDoItAll.Memory.Mcp/*.cs` | `bundle://proof/SB08/transcripts/source-audit-mcp-driver-line-counts.txt` | all MCP driver files remain under 220 lines | memory suite checkpoint fails on overgrown generic memory files |
