# SB07 Semantic Invariants

## Invariant SB07_HTTP001

- Invariant ID: `SB07_HTTP001`
- Source raw note: a simple external HTTP memory provider must receive a plain query-like payload while the host retains the full structured envelope.
- Expected behavior: `HttpMemoryProviderDriver.ExecuteContextQueryAsync` posts query text, requested capability ids, operation id, correlation id, causation id, provider id, capability id, protocol version, and a `MemoryOperationEnvelope<MemoryContextQueryRequest>` to the configured query endpoint.
- Disallowed shallow implementation: sending only a string query, dropping ledger identifiers, or serializing native Cognitive Memory types.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first-http-driver-tests.txt`
- Passing test: `SB07_HTTP001` in `bundle://proof/SB07/transcripts/passing-http-driver-tests.txt`
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.Requests.cs`, `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderContracts.cs`, and `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryHttpDriverTests.cs`
- Production assertions: `bundle://proof/SB07/transcripts/source-audit-http-driver-assertions.txt`
- Red-team negative case: a driver that omits the envelope, authorization header, or operation identifiers fails the focused request capture assertions.
- Downstream dependency check: SB08-SB10 and SB27 can rely on a stable HTTP provider contract without depending on native provider internals.

## Invariant SB07_HTTP002

- Invariant ID: `SB07_HTTP002`
- Source raw note: HTTP providers may complete synchronously with a context pack or asynchronously with an accepted operation.
- Expected behavior: `HttpMemoryProviderResponse.FromContextPack` maps to a `ContextPack` result and `FromAccepted` maps to `OperationAccepted` with ledger status `Running`.
- Disallowed shallow implementation: treating async acceptance as success with no operation handle or collapsing all non-context responses into provider errors.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first-http-driver-tests.txt`
- Passing test: `SB07_HTTP002` in `bundle://proof/SB07/transcripts/passing-http-driver-tests.txt`
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Application/MemoryRuntimeDriverContracts.cs`, `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.Responses.cs`, and `repo://src/Memory/CanDoItAll.Memory.Persistence/MemoryRuntimeService.cs`
- Production assertions: `bundle://proof/SB07/transcripts/source-audit-http-driver-assertions.txt`
- Red-team negative case: an accepted operation without a typed `MemoryOperationAccepted` payload maps to provider error instead of fabricated running state.
- Downstream dependency check: SB09 async workers can consume accepted operations from the generic runtime result contract.

## Invariant SB07_HTTP003

- Invariant ID: `SB07_HTTP003`
- Source raw note: provider timeouts must become observable operation states instead of blocking agent execution.
- Expected behavior: each HTTP operation gets a linked timeout token from provider configuration, and provider timeout maps to `MemoryProviderDriverResultKind.Timeout` and ledger status `TimedOut`.
- Disallowed shallow implementation: relying on default `HttpClient.Timeout`, swallowing cancellations as generic provider errors, or blocking until the remote provider returns.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first-http-driver-tests.txt`
- Passing test: `SB07_HTTP003` in `bundle://proof/SB07/transcripts/passing-http-driver-tests.txt`
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.cs`, `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderConfiguration.cs`, and `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryServiceCollectionExtensions.cs`
- Production assertions: `bundle://proof/SB07/transcripts/source-audit-http-driver-assertions.txt`
- Red-team negative case: delayed provider responses beyond the configured timeout return `TimedOut` instead of completing or hanging.
- Downstream dependency check: SB09 timeout workers can distinguish provider timeouts from provider errors.

## Invariant SB07_HTTP004

- Invariant ID: `SB07_HTTP004`
- Source raw note: caller cancellation must remain cancellation, not a provider timeout or hidden fallback.
- Expected behavior: if the caller cancellation token is signaled, the HTTP driver propagates `OperationCanceledException` and the handler observes a canceled token.
- Disallowed shallow implementation: catching every `OperationCanceledException` and mapping it to timeout, retry, or provider unavailable.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first-http-driver-tests.txt`
- Passing test: `SB07_HTTP004` in `bundle://proof/SB07/transcripts/passing-http-driver-tests.txt`
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.cs` and `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryHttpDriverTests.cs`
- Production assertions: `bundle://proof/SB07/transcripts/source-audit-http-driver-assertions.txt`
- Red-team negative case: canceling the caller token before provider completion throws instead of returning a typed timeout result.
- Downstream dependency check: MAF call sites can use cooperative cancellation semantics without compensating for hidden retry loops.

## Invariant SB07_HTTP005

- Invariant ID: `SB07_HTTP005`
- Source raw note: provider health must be checked through a generic driver contract.
- Expected behavior: `IMemoryProviderHealthDriver.GetHealthAsync` calls the configured health endpoint, reads typed `MemoryProviderHealth` on success, and maps degraded, unreachable, timeout, transport, and malformed responses without native dependencies.
- Disallowed shallow implementation: host-specific health checks, Qdrant/OpenAI checks, or throwing on degraded HTTP health responses.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first-http-driver-tests.txt`
- Passing test: `SB07_HTTP005` in `bundle://proof/SB07/transcripts/passing-http-driver-tests.txt`
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Application/MemoryRuntimeDriverContracts.cs`, `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.cs`, and `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryServiceCollectionExtensions.cs`
- Production assertions: `bundle://proof/SB07/transcripts/source-audit-http-driver-boundary.txt`
- Red-team negative case: a degraded health response remains a typed degraded result and does not fail host startup.
- Downstream dependency check: SB20 provider management UI can surface provider health without taking dependencies on provider internals.

## Invariant SB07_HTTP006

- Invariant ID: `SB07_HTTP006`
- Source raw note: malformed HTTP provider responses and provider errors must become observable generic failures.
- Expected behavior: malformed JSON, empty bodies, mismatched response kind/payload, and non-success HTTP statuses map to `ProviderError`, `Unavailable`, `Timeout`, or `UnsupportedCapability` with actionable diagnostics.
- Disallowed shallow implementation: returning empty context packs on malformed responses, throwing unclassified JSON exceptions, or silently retrying non-idempotent provider work.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first-http-driver-tests.txt`
- Passing tests: `SB07_HTTP006`, `SB07_HTTP007`, and `SB07_HTTP008` in `bundle://proof/SB07/transcripts/passing-http-driver-tests.txt`
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.Responses.cs`, `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderContracts.cs`, and `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryHttpDriverTests.cs`
- Production assertions: `bundle://proof/SB07/transcripts/source-audit-http-driver-assertions.txt`
- Red-team negative case: 501 maps to unsupported capability, 503 maps to unavailable, and malformed success JSON maps to provider error.
- Downstream dependency check: SB15 shared handlers and SB21 feedback UI can reason over typed provider failures.

## Invariant SB07_HTTP007

- Invariant ID: `SB07_HTTP007`
- Source raw note: HTTP driver configuration must be provider-profile driven and opt-in.
- Expected behavior: the HTTP project exposes strongly named extension keys, provider endpoint constants, and `AddHttpMemoryProviderDriver`; it is not registered as a base fallback and does not pull native memory, Qdrant, OpenAI, RAG, EF, or host infrastructure dependencies.
- Disallowed shallow implementation: hard-coded endpoint strings in call sites, default native fallback, or host composition changes that make HTTP/native memory mandatory.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first-http-driver-tests.txt`
- Passing proof: `bundle://proof/SB07/transcripts/source-audit-http-driver-boundary.txt`, `bundle://proof/SB07/transcripts/passing-memory-test-suite.txt`, and `bundle://proof/SB07/transcripts/passing-solution-build.txt`
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderConfiguration.cs`, `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryServiceCollectionExtensions.cs`, and `repo://src/Memory/CanDoItAll.Memory.Http/CanDoItAll.Memory.Http.csproj`
- Production assertions: `bundle://proof/SB07/transcripts/source-audit-http-driver-boundary.txt` and `bundle://proof/SB07/transcripts/source-audit-http-driver-anti-stub.txt`
- Red-team negative case: dependency audits fail if the HTTP driver references native Cognitive Memory, Qdrant, OpenAI, RAG, EF, `AppDbContext`, or infrastructure projects.
- Downstream dependency check: SB30 can keep base host startup free of native provider dependencies while still enabling HTTP providers explicitly.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| HTTP memory provider project | `repo://src/Memory/CanDoItAll.Memory.Http/CanDoItAll.Memory.Http.csproj` | `bundle://proof/SB07/transcripts/passing-solution-build.txt` | opt-in `AddHttpMemoryProviderDriver` registration | dependency boundary audit rejects native, Qdrant, OpenAI, RAG, EF, and infrastructure references |
| HTTP request/envelope mapping | `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.Requests.cs` | `SB07_HTTP001` in `bundle://proof/SB07/transcripts/passing-http-driver-tests.txt` | posted request contains plain query fields and full structured envelope | request capture fails if operation/correlation/capability/protocol metadata is omitted |
| HTTP response mapping | `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.Responses.cs` | `SB07_HTTP002`, `SB07_HTTP006`, `SB07_HTTP007`, and `SB07_HTTP008` | maps context pack, accepted operation, provider error, timeout, unavailable, and unsupported capability | malformed payloads and unsupported capability do not become empty success |
| Timeout and cancellation behavior | `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.cs` | `SB07_HTTP003` and `SB07_HTTP004` | per-operation linked timeout token with caller cancellation preservation | caller cancellation throws; provider timeout maps to `TimedOut` |
| Provider health driver | `repo://src/Memory/CanDoItAll.Memory.Application/MemoryRuntimeDriverContracts.cs` and `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.cs` | `SB07_HTTP005` | health reads typed generic provider health | degraded/unreachable health stays generic and native-free |
| Runtime driver result extension | `repo://src/Memory/CanDoItAll.Memory.Application/MemoryRuntimeDriverContracts.cs` and `repo://src/Memory/CanDoItAll.Memory.Persistence/MemoryRuntimeService.cs` | full memory suite in `bundle://proof/SB07/transcripts/passing-memory-test-suite.txt` | accepted operations flow through runtime result and ledger status | accepted operations cannot be represented as anonymous strings or fake context packs |
| Cohesive driver split | `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.cs`, `.Requests.cs`, and `.Responses.cs` | `bundle://proof/SB07/transcripts/source-audit-http-driver-line-counts.txt` | all HTTP driver files remain under checkpoint line-count cap | full memory suite checkpoint fails on overgrown files |
