# SB07 Proof Manifest

## Status

- Subbundle: `SB07`
- Status: `Completed`
- Owned requirements: `R03`, `R04`, `R17`
- Owned raw notes: HTTP memory provider driver, typed request and response mapping, provider-profile HTTP configuration, auth header policy, timeout/cancellation behavior, health checks, and native-free HTTP transport boundary.

## Semantic Invariant Contract

- Contract: `bundle://proof/SB07/semantic-invariants.md`

## Changed File Hashes

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://CanDoItAll.slnx` | `fc3c726b4fce869fb9aa2fe1aac1f091fc4e356ba762622738d0e2143b231ebd` | `d6c58ff00bddbd2b388c19673adabe24387399522091a0962c7e448ed37eae18` |
| `repo://src/Memory/CanDoItAll.Memory.Application/MemoryRuntimeDriverContracts.cs` | `5b62c55baad5f207a58c8fe95a7fb8df0d7d55821063f9706790a4da24a74379` | `9447a712a06168e4bbe8a16b7dc43c4d7f8210a77ffbe0807a87ee68f4705b8c` |
| `repo://src/Memory/CanDoItAll.Memory.Persistence/MemoryRuntimeService.cs` | `b45e40f1e9a690f6addc38b321e0e300187c389e1d5ece06d2cbc4793123a6a1` | `8be84c99d5d25327b6760d0a792482e76ba18b738c3e2985708c50b144b51a4a` |
| `repo://src/Memory/CanDoItAll.Memory.Persistence/DeterministicMockMemoryProviderDriver.cs` | `1a3e1b365952906f3c1e66f55a70fae4f615f338d7f0b8290a7c016264669b93` | `f2e5eacad72d96c72fde23fe274ea4dfe513a16d8672b2481450649d39a96c59` |
| `repo://src/Memory/CanDoItAll.Memory.Http/CanDoItAll.Memory.Http.csproj` | `<new file>` | `d55e91c6ffdc7efe98d16d70d277c280e750099aa0edfeff65aeab9060ddd7ba` |
| `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderOptions.cs` | `<new file>` | `f4cfdc6d25c36046a8f516647ce69fa3c2e2703145455ce06c73e9df405052ce` |
| `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderConfiguration.cs` | `<new file>` | `9f1f47b5f9bbd0a29a5d0dd275ae82b3bb0f2da91ccaf13d2f723a9def9871c5` |
| `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderContracts.cs` | `<new file>` | `0d9778c46665c51ea6f795e5e5719ce4dbe00d4a343cf99a108d6f1aab130bc2` |
| `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.cs` | `<new file>` | `0459afa4d0b808321f1fb0e0f5bb6c38cf084b17513d8806c6050e60fbe56e7b` |
| `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.Requests.cs` | `<new file>` | `1f7ce41c3bda7cfbad2172a7545317a0bb4100df1072f2cdd63c3f828c13247c` |
| `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.Responses.cs` | `<new file>` | `29e20e2a4baad913fb75150d2994d296a96f6ab2133cc7c69ba2fb6a9fb552af` |
| `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryServiceCollectionExtensions.cs` | `<new file>` | `d7a6a0bbdf1d5e350467aee37ba1240dd7e155059e924c03547902b4031b0aa8` |
| `repo://tests/Memory/CanDoItAll.Memory.Tests/CanDoItAll.Memory.Tests.csproj` | `6e8aa47498dbfd2b73d4e19694a277f9006f621efa1c04050301213ffbd42fb0` | `1f1c58f2647a82e6ecdd7b5c1676b76beb369c5ee8bd212ef1176f958d9d6873` |
| `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryHttpDriverTests.cs` | `<new file>` | `0de31a8a02df7ba26fd8e734b92ed76323bc8e5425a2b4b4705306b232e5b8b7` |
| `repo://codex/bundles/candoitall-memory-provider-extraction-bundle/proof/SB07/transcripts/failing-first-http-driver-tests.txt` | `<new file>` | `0563c6415f8215a342742004acb4664895f2344a3ec5667dd59532578e673590` |
| `repo://codex/bundles/candoitall-memory-provider-extraction-bundle/proof/SB07/transcripts/passing-http-driver-tests.txt` | `<new file>` | `c8960a9891ac420374cc1156c8e62159db1a8612e36d4c4d8e55487574c541fc` |
| `repo://codex/bundles/candoitall-memory-provider-extraction-bundle/proof/SB07/transcripts/passing-memory-test-suite.txt` | `<new file>` | `5dccb70dceb1f385b7083a0eb881e0e9b1dc485c6b79ed5b207efc9e2e49c02b` |
| `repo://codex/bundles/candoitall-memory-provider-extraction-bundle/proof/SB07/transcripts/passing-solution-build.txt` | `<new file>` | `692e914e8eae1d5a8ce27d3adcdeccb953f4284590f642a4032597fe69a84ca5` |
| `repo://codex/bundles/candoitall-memory-provider-extraction-bundle/proof/SB07/transcripts/source-audit-http-driver-boundary.txt` | `<new file>` | `1c8e6ce09d8415ed5c85dfe9a165658bf2e94c6ad3f0da165e964725f82a1e15` |
| `repo://codex/bundles/candoitall-memory-provider-extraction-bundle/proof/SB07/transcripts/source-audit-http-driver-assertions.txt` | `<new file>` | `baecd0b23a8ae55014d58c73aa11b054975a1a772ac56bac2c9f444cc326e157` |
| `repo://codex/bundles/candoitall-memory-provider-extraction-bundle/proof/SB07/transcripts/source-audit-http-driver-anti-stub.txt` | `<new file>` | `8136163e96f2be102249e9cc9d89c036ce16ca68e37c072cb2cdb9bdb7671ba0` |
| `repo://codex/bundles/candoitall-memory-provider-extraction-bundle/proof/SB07/transcripts/source-audit-http-driver-line-counts.txt` | `<new file>` | `3fc728670d74733ea6e6cd651eb5e66bd12c2615a751fbd05d6c7d993169d4f6` |
| `repo://codex/bundles/candoitall-memory-provider-extraction-bundle/README.md` | `bca1fcb041e129bb54c05a1322d5e1fdbc846d4820a40e289ef9a6ad3906e487` | `4bca5530011050eaf636e4230921bdcc0d211825a0eac98419ae922a7548fe4c` |
| `repo://codex/bundles/candoitall-memory-provider-extraction-bundle/reviews/01-execution-report.md` | `9677f0cd260eb14e100b6d57ae276b26e980e236f795d7a6132c58e7fa263b10` | `7dfe85f21b108192e39e6922aee4e3843e07ee61005384e91d32170baea5e49c` |
| `repo://codex/bundles/candoitall-memory-provider-extraction-bundle/subbundles/07-http-driver-and-resilience-policies/README.md` | `<updated by SB07>` | `12466115405efbdf01c50736aa24e996d2965a597a76097b0a6d3c04bc90aa76` |
| `repo://codex/bundles/candoitall-memory-provider-extraction-bundle/proof/SB07/semantic-invariants.md` | `<new file>` | `e12449230decf5cec432983561cb6b84ab28008e7ab20d6a324d6261c8f5f4aa` |
| `repo://codex/bundles/candoitall-memory-provider-extraction-bundle/evidence/12-prepared-stage-validation-after-sb07.txt` | `<new file>` | `443eeefc2a9c72b3061984f244965401e210cc6b3a7c79f5b3b456fdeb185b9d` |

## Command Transcripts

| Purpose | Transcript |
| --- | --- |
| Failing-first HTTP driver tests | `bundle://proof/SB07/transcripts/failing-first-http-driver-tests.txt` |
| Passing focused HTTP driver tests | `bundle://proof/SB07/transcripts/passing-http-driver-tests.txt` |
| Passing full memory test suite | `bundle://proof/SB07/transcripts/passing-memory-test-suite.txt` |
| Solution build | `bundle://proof/SB07/transcripts/passing-solution-build.txt` |
| HTTP driver dependency boundary audit | `bundle://proof/SB07/transcripts/source-audit-http-driver-boundary.txt` |
| HTTP driver source assertion audit | `bundle://proof/SB07/transcripts/source-audit-http-driver-assertions.txt` |
| Anti-stub audit | `bundle://proof/SB07/transcripts/source-audit-http-driver-anti-stub.txt` |
| HTTP driver line-count audit | `bundle://proof/SB07/transcripts/source-audit-http-driver-line-counts.txt` |

## Failing-First Proof

- Transcript: `bundle://proof/SB07/transcripts/failing-first-http-driver-tests.txt`
- Result: non-zero exit after adding SB07 HTTP driver tests and before adding the HTTP driver project and runtime driver result extensions.
- Failure observed: missing `CanDoItAll.Memory.Http` project, missing HTTP driver types, and missing `IHttpClientFactory`-based registration surface.
- Invariant IDs covered by later passing tests: `SB07_HTTP001`, `SB07_HTTP002`, `SB07_HTTP003`, `SB07_HTTP004`, `SB07_HTTP005`, `SB07_HTTP006`, `SB07_HTTP007`, and `SB07_HTTP008`.

## Passing Proof

- Transcript: `bundle://proof/SB07/transcripts/passing-http-driver-tests.txt`
- Command: `dotnet test tests\Memory\CanDoItAll.Memory.Tests\CanDoItAll.Memory.Tests.csproj --filter "FullyQualifiedName~MemoryHttpDriverTests"`
- Result: exit code `0`, eight focused SB07 tests passed.
- Test names: `SB07_HTTP001_Sync_context_pack_posts_plain_query_payload_structured_envelope_and_auth`, `SB07_HTTP002_Async_accepted_response_maps_to_running_operation`, `SB07_HTTP003_Provider_timeout_uses_per_operation_budget_and_maps_timed_out`, `SB07_HTTP004_Caller_cancellation_propagates_without_timeout_mapping`, `SB07_HTTP005_Health_driver_returns_degraded_provider_health`, `SB07_HTTP006_Malformed_response_maps_provider_error`, `SB07_HTTP007_Unsupported_capability_response_is_typed`, and `SB07_HTTP008_Unavailable_status_maps_unavailable_without_retry_by_default`.

## Compatibility Proof

- Transcript: `bundle://proof/SB07/transcripts/passing-memory-test-suite.txt`
- Command: `dotnet test tests\Memory\CanDoItAll.Memory.Tests\CanDoItAll.Memory.Tests.csproj`
- Result: exit code `0`, all 43 memory tests passed across SB01-SB07.
- Architecture guard compatibility: the SB05 file-size checkpoint remained active; the HTTP driver was split into bounded partial files after the full suite initially caught an overgrown 325-line driver file.

## Source Assertions

- Dependency boundary audit: `bundle://proof/SB07/transcripts/source-audit-http-driver-boundary.txt`
- Behavior assertion audit: `bundle://proof/SB07/transcripts/source-audit-http-driver-assertions.txt`
- Line-count audit: `bundle://proof/SB07/transcripts/source-audit-http-driver-line-counts.txt`
- Source proof covers `IHttpClientFactory` registration, `AddHttpClient`, auth header policy, `MemoryOperationEnvelope` request mapping, per-operation timeout token creation, caller cancellation propagation, health driver contract, accepted operation support, typed timeout/unavailable/unsupported classifications, and no native provider dependency leakage.

## Anti-Stub Audit

- Transcript: `bundle://proof/SB07/transcripts/source-audit-http-driver-anti-stub.txt`
- Result: no `TODO`, `NotImplemented`, placeholder, fixture-specific, default-return, or null-return stub markers in SB07 HTTP driver, runtime result contract, runtime service, or focused test paths.

## Downstream Smoke Proof

- `bundle://proof/SB07/transcripts/passing-solution-build.txt` proves the new HTTP driver project and runtime result contract compile in `repo://CanDoItAll.slnx`.
- Build result: exit code `0`, with known NU1900 NuGet vulnerability-index warnings and NU1903 `Microsoft.OpenApi` advisory warnings only.
- `bundle://evidence/12-prepared-stage-validation-after-sb07.txt` proves the bundle still passes prepared-stage validation after SB07 closure.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| HTTP driver project | `repo://src/Memory/CanDoItAll.Memory.Http/CanDoItAll.Memory.Http.csproj` | `bundle://proof/SB07/transcripts/passing-solution-build.txt` | solution membership and opt-in DI registration | dependency boundary audit rejects native, Qdrant, OpenAI, RAG, EF, and infrastructure references |
| Provider profile HTTP configuration | `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderConfiguration.cs` | `SB07_HTTP001` through `SB07_HTTP005` | profile extension keys control base URL, paths, auth, timeout, and retries | invalid/missing required base URL throws explicitly |
| HTTP request mapping | `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.Requests.cs` | `SB07_HTTP001` | plain query payload and full structured envelope are posted together | tests fail if ids, protocol version, capability id, or auth header are omitted |
| HTTP response mapping | `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.Responses.cs` | `SB07_HTTP002`, `SB07_HTTP006`, `SB07_HTTP007`, and `SB07_HTTP008` | sync pack, async accepted, malformed, provider error, timeout, unavailable, and unsupported states are classified | malformed responses do not become empty success |
| Timeout/cancellation behavior | `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.cs` | `SB07_HTTP003` and `SB07_HTTP004` | linked timeout token per operation, caller cancellation preserved | caller cancellation throws; provider timeout maps to `TimedOut` |
| Health driver | `repo://src/Memory/CanDoItAll.Memory.Application/MemoryRuntimeDriverContracts.cs` and `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.cs` | `SB07_HTTP005` | generic health endpoint reads typed `MemoryProviderHealth` | degraded/unreachable health stays typed and does not require native internals |
| Runtime accepted operation result | `repo://src/Memory/CanDoItAll.Memory.Application/MemoryRuntimeDriverContracts.cs` and `repo://src/Memory/CanDoItAll.Memory.Persistence/MemoryRuntimeService.cs` | `SB07_HTTP002` and full memory suite | runtime propagates accepted operations and ledger status | accepted operations cannot be hidden as string diagnostics |
| Cohesion checkpoint compliance | `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.cs`, `.Requests.cs`, and `.Responses.cs` | `bundle://proof/SB07/transcripts/source-audit-http-driver-line-counts.txt` | all HTTP source files stay under 220 lines | memory suite failed before the split and passes after it |

## Browser Validation

- Browser validation: `N/A`.
- Reason: SB07 added generic HTTP driver contracts, transport implementation, DI registration, and tests only. It did not add host routes, browser-visible UI, or provider management rendering.

## Closure Decision

- SB07 closure gate: `Pass`.
- Reopened subbundles: `None`.
- Host validation: `N/A`; host composition and provider-management UI are handled by later subbundles.
- Downstream permission: SB08 MCP driver and driver factory model may start after bundle-level validation passes.
