# Proof Manifest — SB04

- Status: `Completed`.
- Proof tier: `Governed`.
- Owned requirements: `SCUI-018`, `SCUI-019`, `SCUI-020`, `SCUI-021`, `SCUI-023`, `SCUI-024`, `SCUI-025`, `SCUI-058`, `SCUI-062`.
- Start commit: `a0732674a859bf46a76d49efd245dd91681575fd`.
- Candidate commit: `ccc476668bc67d9cb217daa3f3935de91294849f` (`feat(llm-chats): expose active operation identity`).
- SharedInfo commit/hash: `7b7808e8591d7219f40826cf0e5624e182981d90`.
- Semantic contract: `bundle://proof/SB04/semantic-invariants.md`.
- Architecture decision: `bundle://proof/SB04/architecture-gate.md`.
- Execution report: `bundle://proof/SB04/execution-report.md`.

## Scope

SB04 adds the exact typed active-operation identity to authoritative LlmChats state and the additive HTTP projection, proves terminal clearing and ownership/profile fences, and characterizes the durable event-session lifetime required for reconnect. It does not activate Simple Chat UI, alter floating Agent UI, add later product capabilities, run Stable, or run Playwright.

## Changed source integrity

Hashes are SHA-256 over exact file bytes at the start and candidate commits.

| Path | Before | After |
|---|---|---|
| `repo://src/App/CanDoItAll.Web/Api/LlmChatApiContracts.cs` | `4e31c7d00ba165db38f43a26767721d6ae7587b0df6ec2945a8566f40e166500` | `899f77497849475214c6a0a6868cb03887eb14cdccf60189df15f042a9a2ab0e` |
| `repo://src/App/CanDoItAll.Web/Api/LlmChatApiMapper.cs` | `e6d6ac475057c8328881da3dddd1d084e99850e36b0bca8e07cc448e81431a64` | `4a8de1b907712cc441206fd5531915151a036e4d5b3ffba8eb502f79fa3bd818` |
| `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/ReadModels/EfLlmChatConversationReadStore.cs` | `c119211455e9dad29cce99fceacb3a6eedcac1eef0f5a0497d5119d63211cf00` | `0bfebb9e35c84d61f5e8e5e2cd07600772d4a29ddb021b9af3f532ba7671640d` |
| `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/LlmChatConversationEngine.cs` | `8b179ef58e43d9d7f9371cca3e03ee7d556f77a7917f4d842cbddc9b047d2480` | `d5c76c08cb298fd3e6562a22f54206806c9b9e29b97a076265793cff51c280e1` |
| `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatConversationContracts.cs` | `4a7e3233fb1aa1de944c51f5f990f21ef243e295adedb39fcf955d609ab04e44` | `5a1f11b1dd06c06911ddd18ff33c6c82187fc4bba7b0454d8f6adb51bf51939b` |
| `repo://src/Modules/CanDoItAll.Modules.LlmChats/Ports/LlmChatExecutionPorts.cs` | `20f8daf7abd2a998196cef4757eef84e76f73b1f9c5b1ee574a10deb3be16ce1` | `31c8894efb70227bbee4a27bbbcacaf71fe5dd39dc00e12507b778882f3a528f` |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatPersistenceIntegrationTests.cs` | `4eae46db9d435414e18e1b37b125e4c6118d9b1d61a7d32a8aec12f26fde9a96` | `04f43a1e76264a03539eec54c8437f59d629bd05c4ec0eb8baaac81ce188ee62` |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatTransactionalConcurrencyIntegrationTests.cs` | `bb88c52bb97e31c5a5ff74931dcda65071e323dda743a434e16e7a9770d5d863` | `c76b78d0e30ca94680d52bff958a38ba6aad80facc65fa5bfde7c819c68823af` |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatsApiIntegrationTests.cs` | `2e1f52f39a76c1db757c9d355e7e97aa085a146be22f09884ddf8cb3487907c7` | `0c4d62468252840262e6cd94dc1657fffdaddea862d5663b40287b4eb1ec8070` |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatsApiPostgreSqlIntegrationTests.cs` | `693486d7c49b991c7d265716f06c200f4348761591ff45e4e55407b791c1e7f2` | `6e1bef783cb1a138585cd52ca574c5360f8824329b1a5a1d60e9b8118dad4b1b` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/LlmChatApplicationTestDoubles.cs` | `beccbe86413f13f7b1d16ed989c2c3f79933e084a9e3491929cfceb397e1bb06` | `6a6642bdac00b995a073a0d9c506db08040c60224a2e11f80faa2b2aece43c24` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/LlmChatDurableStreamEventTests.cs` | `9b0343db9745b83866a4ffc2a2c1d15614fe715cb7b5198cf17bd28817275be6` | `649581907514b335303b701eb5bd15cae203a56ab7ccc4a3a073ead6342f1c00` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/LlmChatOperationTestDoubles.cs` | `af885c37101e30d3fb3d51f73182992e2cce5600fe7c0deb953da9998c592b41` | `35ac5631856d6f16168a3d4297be2abbff79131c6a78d56b6aae86b33c8c16e9` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/LlmChatProviderRuntimeTests.cs` | `551060e8e70358897fdc47e635bf3d1f729aba32c45db7188d98110989d0e73b` | `52a9fb9d136046ffd83aca0146c129a0daabad96552ddd728a02aba9a06346ad` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/LlmChatWholeUseCaseProfileScopeTests.cs` | `1df4a91fd3373c51d90d2e4910a55b16c1ce9e2aed6c4aa45efc7b11a83e0240` | `7148daeb36c07bab901fb65e10f470308ecffe34f1f3772d21d225837d0151b4` |

## Bundle and proof integrity

| Path | Before | After |
|---|---|---|
| `bundle://subbundles/SB04-active-operation-reconnect-contract/README.md` | `bedc41d04526fb35ddce260fa1c579fe05c5afd2c95eb41d237d347291d687c5` | `1e1f97f64a84f414a8661d41b4846bebf4360c2151c207b51d8fbad4ab8333e2` |
| `bundle://bundle-status.json` | `2750a226d78d50b6de46ff6cd64b94c518496c9ce8c0b3a25e20fd0cb39e447` | `0db3e3f51e1630f032529faccfb8052197d4ead85e2660aba5c59a92b48fddf3` |
| `bundle://reviews/01-execution-report.md` | `e3b86a9e9eb74873a2c000ee8540a8c03e79b194ed640e971a307b3bd9a359e` | `12a18ab3c2e29f25f19c4d90f780f5c5aa9f702185665f7f4718c816880ad99f` |
| `bundle://proof/SB04/architecture-gate.md` | absent | `c0186918ac1003bbf7b4e232492868d93a6190c7c4aa62172d05a93e2d7784b2` |
| `bundle://proof/SB04/execution-report.md` | absent | `83b243ea7defa0089d640240ed6af16fe73358c965c63a675cc779258bea0348` |
| `bundle://proof/SB04/semantic-invariants.md` | absent | `0c7341baf4e5e7fea90e8c780e8b5894cf6c7ea9f36d54ce917d095a20406a4d` |
| `bundle://proof/SB04/transcripts/01-failing-first-active-operation.md` | absent | `a095f90eb657fd14516b36edd210045b3d5707483d3b1d138edf6fa6bdb4aeda` |
| `bundle://proof/SB04/transcripts/02-characterization-event-session.md` | absent | `f08c8f12c4e582543e50d5dc37fedde869e4ea47804323540cc64ab65cebed57` |
| `bundle://proof/SB04/transcripts/03-focused-unit.md` | absent | `e839f57aec638b9caec66cf5389dfe965ef69442f922ab9ce17d59a0799aee8f` |
| `bundle://proof/SB04/transcripts/04-focused-api.md` | absent | `36bfbbcfa404cb410c3a8c20ff710c5d16a829f6d18897959b55c004f1d243e4` |
| `bundle://proof/SB04/transcripts/05-focused-postgresql.md` | absent | `c1b596524aa2194fd315711d1aa641baceec6d1dc43cc87455be594af7f89374` |
| `bundle://proof/SB04/transcripts/06-impact-analysis.md` | absent | `523341183f8eca604ccc189ec0b0b2f239e37ce36697de9985388d9f10a3a706` |
| `bundle://proof/SB04/transcripts/07-required-unit-workspace.md` | absent | `3210db52376c528db56370fcf8257f19f249eb30c99d45122b32e73c176fea13` |
| `bundle://proof/SB04/transcripts/08-required-integration-workspace.md` | absent | `a1ba73113a6c1497b81f3ecb8ca54da8796b04e3a12420c12f275155da3ce19a` |
| `bundle://proof/SB04/transcripts/09-builds-static.md` | absent | `4c3bb7b2723420e599e2810ab366353a430be3dd14e81a823332faf53d540b87` |
| `bundle://proof/SB04/transcripts/10-anti-stub.md` | absent | `4230fefda5630acc616274f0682a149cf55dcad2c2ecc1a9547e67519dae64f4` |
| `bundle://proof/SB04/transcripts/11-source-assertions.md` | absent | `76ccc04071785a9a2ea3a26f6e92610280aa04135f4bd08c51ad591b79c222a7` |

The manifest omits its own self-referential digest. Its integrity is checked by the bundle validator and the proof commit.

## Validation and artifact matrix

- Failing-first: the new contract selector failed with five expected missing-member compiler errors before production changes.
- Focused Unit: active identity, all terminal paths, cross-conversation/profile fences, follower disposal, gap semantics, and cancellation selectors passed.
- Focused API/PostgreSQL: exact active HTTP identity, inactive omission, lifecycle, profile-switch, reconnect, and transfer selectors passed.
- CodeAnalytics: correlation `code-analytics_6faa6d0071ef4cb3b73e504c4dfacbf7`; healthy Unit/Integration workspaces, 5,745 source tests, low-confidence `AllSuppliedSuites` fallback due `TIA2001`, `TIA3002`, and `TIA3004`.
- Full Unit: 6,229 passed, 0 failed, 0 skipped.
- Full Integration: 851 passed, 3 failed, 1 expected live-Ollama skip. All three failures reproduce outside SB04 selectors and are documented in `bundle://proof/SB04/transcripts/08-required-integration-workspace.md`.
- Builds: LlmChats, LlmChats.Persistence, and Web passed with zero warnings and errors; `git diff --check` passed.
- Anti-stub/source assertions: `bundle://proof/SB04/transcripts/10-anti-stub.md` and `bundle://proof/SB04/transcripts/11-source-assertions.md`.
- Architecture: snapshot `snap-20260816171034-d26d371e`; dependency query `code-analytics_861387a454f1457eb32144bb86ea6b05`; no project reference or cycle change. Two pre-existing AgentFramework cycles are unchanged.
- Browser method/viewport: not applicable and forbidden by SB04.

## Acceptance and progression

All four acceptance criteria pass: exact active identity, terminal/compensation/abandonment clearing, ownership/profile isolation, and additive HTTP compatibility. Reopen on any owned contract, mapper, route, authorization, lifecycle, or adapter change, any required-selector regression, a new dependency cycle, missing proof, or later browser-parity regression. SB05 is unlocked; CP1 remains locked.
