# SB03 Governed Preparation Snapshot Proof Manifest

## Identity

- Subbundle: `SB03 Revisioned Runtime Preparation Snapshots`
- Status: `Complete — A3 Pass with two A3 P2 follow-ups`
- Date: `2026-07-27`
- Owned requirements: R07 and the preparation half of R08.
- Raw-note ownership: prepare reusable agent/runtime data without retaining live
  runtime instances; use safe parallel read work only where dependencies are
  independent; snapshots require explicit lifetime and update policy.
- Decision: `bundle://proof/SB03/a3-decision.md`
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`
- Architecture snapshot: `bundle://proof/SB03/architecture-snapshot.md`
- Provider lifecycle matrix: `bundle://proof/SB03/provider-snapshot-lifecycle.md`
- Startup operation and timing evidence: `bundle://proof/SB03/startup-metrics.md`

## Evidence

| Evidence | Result | Artifact |
| --- | --- | --- |
| Focused preparation/provider-snapshot unit suite | Pass, 13/13 | `bundle://proof/SB03/transcripts/passing-provider-snapshot-unit-13.txt` |
| PostgreSQL startup matrix | Pass, 4/4 | `bundle://proof/SB03/transcripts/passing-startup-baseline-4.txt` |
| Owned AgentFramework production compile | Pass, 0 errors, 85 existing NU1903 warnings | `bundle://proof/SB03/transcripts/passing-agentframework-module-build.txt` |
| Production anti-stub scan | Pass, no matches | `bundle://proof/SB03/transcripts/anti-stub.txt` |
| Full Composition retry during parallel SB04 work | Blocked by eight transient SB04-owned compile errors; preserved, not misreported as an SB03 failure or pass | `bundle://proof/SB03/transcripts/composition-concurrent-sb04-blocker.txt` |
| Current focused architecture unit suite | Pass, 140/140 parent-confirmed; original console stream not retained | Confirmed validation handoff; not represented as a raw transcript |
| Downstream backend gate | `GO with three P2 follow-ups`; deterministic operation/count and concurrency proof | `bundle://proof/SB05/a5-decision.md`, `bundle://proof/SB05/operation-counts.md`, `bundle://proof/SB05/concurrency-invariants.md` |
| Final architecture snapshot | Pass; affected project graph acyclic | `snap-20260728014834-63e19a8b`, `bundle://reviews/csharp-architecture-gate.md` |

- Failing-first transcript: `bundle://proof/SB03/transcripts/controlled-stale-completion-red-green.txt`.
- Passing transcript: `bundle://proof/SB03/transcripts/controlled-stale-completion-red-green.txt`.
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/anti-stub.txt`.

## Owned source slice

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Preparation/AgentExecutionPreparationCache.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Preparation/ProviderRuntimeProfileSnapshot.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Preparation/AgentExecutionPreparationService.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Preparation/AgentExecutionPreparationModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ReferenceData/AgentReferenceDataCache.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ReferenceData/AgentReferenceDataContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ReferenceData/AgentReferenceDataInvalidationHub.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ReferenceData/WorkspaceBackedAgentReferenceDataProvider.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Providers/WorkspaceBackedProviderProfileRegistry.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentFrameworkWorkspaceService.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workspace/Services/WorkspaceProviderProfileCommitObserver.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceAgentProviderProfileMapper.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/ProviderRuntimeProfileSnapshotService.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/DatabaseRuntimeAgentExecutionProfileGenerationSource.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `repo://src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Properties/AssemblyInfo.cs`

## Owned test slice

- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentExecutionPreparationCacheTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentExecutionPreparationServiceTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProviderRuntimeProfileSnapshotServiceTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/DatabaseRuntimeAgentExecutionProfileGenerationSourceTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentProviderCredentialDispatchScopeTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ManagedSeedExecutionCredentialBoundaryTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentReferenceDataProviderTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs`

## Semantic adequacy

- Shallow-pass trap: rename the old preparation pool, keep request cancellation tied
  to shared work, or cache a mutable provider/session object while reporting a warm
  hit.
- Adversarial negative: invalidation during a load fences the stale completion;
  cancelling one waiter leaves shared work available; profile/provider revision,
  deletion, probe failure, and committed projection failure reject stale state
  explicitly.
- Semantic positive: same-key/same-version callers share one immutable load and receive
  typed `Refreshed`/`Reused` dispositions; warm dispatch revalidates the canonical
  provider without a provider-registry/database reload.
- Anti-stub result: the production cache, preparation service, provider publication,
  commit observer, profile-generation source, dispatch credential scope, and DI
  composition carry the behavior. No fixture-only branch or template output carries
  the pass.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Immutable preparation blueprint | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Preparation/AgentExecutionPreparationService.cs` | startup in `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs` | cache lifecycle in `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Preparation/AgentExecutionPreparationCache.cs` | `bundle://proof/SB03/transcripts/controlled-stale-completion-red-green.txt` |
| Canonical provider lease | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/ProviderRuntimeProfileSnapshotService.cs` | dispatch test `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentProviderCredentialDispatchScopeTests.cs` | fencing test `repo://tests/Unit/CanDoItAll.Tests.Unit/ProviderRuntimeProfileSnapshotServiceTests.cs` | failure tests indexed by `bundle://proof/SB03/a3-decision.md` |
| Defensive reference-data snapshot | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ReferenceData/WorkspaceBackedAgentReferenceDataProvider.cs` | consumer test `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentReferenceDataProviderTests.cs` | cache lifecycle in `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ReferenceData/AgentReferenceDataCache.cs` | cancellation/retry tests indexed by `bundle://proof/SB03/a3-decision.md` |
| Transient startup aggregate | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` | runtime construction in `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs` | invocation lifetime test `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs` | operation assertions in `bundle://proof/SB03/startup-metrics.md` |

## Governed evidence provenance

SB01 records the pre-change red contracts in
`bundle://proof/SB01/deferred-characterization-contracts.md`. The original
failing-first console stream for the final A3 cache tests was not retained. A fresh,
explicit controlled mutation now proves that removing the stale-entry commit fence
kills the focused test in
`bundle://proof/SB03/transcripts/controlled-stale-completion-red-green.txt`; it is not
presented as the missing historical stream. The current 140/140 result remains a
confirmed parent handoff without its original console stream. This manifest preserves
that limitation rather than reconstructing an original command.

The essential new source/test files are absent at repository `HEAD`; current exact
file-byte SHA-256 values are:

| Artifact | Before SHA-256 | Current SHA-256 |
| --- | --- | --- |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Preparation/AgentExecutionPreparationCache.cs` | `ABSENT` | `4A181464A2560B8A65C53FFD298545ECDCD821EC507255E23086F64CC9D51EE9` |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Preparation/AgentExecutionPreparationService.cs` | `ABSENT` | `E1829E45C845A1A592EABC310A83E8DD2AF50A0D1FC2F42DE49B420D23788170` |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Preparation/ProviderRuntimeProfileSnapshot.cs` | `ABSENT` | `F0663B1DB4A6D918BAC4C601886DE71BBB2F84857617163C6119251F1DC94CB7` |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Preparation/AgentExecutionPreparationModels.cs` | `ABSENT` | `EF73AC5C8303B736D33E919B29ACBB910D87F78E120A49AF138A00330D5D990E` |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/ProviderRuntimeProfileSnapshotService.cs` | `ABSENT` | `DC55FF6B7A1E8FF1D0412BC3B306CC81541E85BAEE5401DF6052380F2EF38260` |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/DatabaseRuntimeAgentExecutionProfileGenerationSource.cs` | `ABSENT` | `D27C1B7114B04DC5143EB787A460DC591E914A9F122B51459A6A3310A182957F` |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ReferenceData/AgentReferenceDataCache.cs` | `6B573B6C5257E6F95460BA4DFA7451048E78E2E63D422C6A4050DAA7ADC34DD5` | `B46C7320700266134EF4C6A352998D3657B54031B7B3BCFB6E6A8F93CB6F64CE` |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ReferenceData/AgentReferenceDataContracts.cs` | `009555AE640DE5CE0CCAB7B4641CF26CC18B8B511210CA64195D8FBD2DB86846` | `0524EFC98350552214BAAA81C59A3CB6832015A3DB4BD22214C3A73552297AFA` |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ReferenceData/AgentReferenceDataInvalidationHub.cs` | `7A2F24206038B03F46C74669A4D5C71DE72474C8AD965F8F2AD7F2CFD8B95D32` | `4A59288D3165B0C3576CDDABCD92F4CDE62AF47EA8982F0CC58A5FACA57D9E8E` |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ReferenceData/WorkspaceBackedAgentReferenceDataProvider.cs` | `85408A7E94B2AACB940B524B8546A772D204339924B0839BE43D2713A4FEB960` | `E422B088C276FC1BE6019F70E54F98DE80AC8E2E14FAA5C4C7C77AADD193A404` |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs` | `514273DAE33156C4BA44CB7E2C7F6F80A08D6D5207E5340783E39250A56551C9` | `7F8F6CB4B3A82EF73312B8AD13D3A4E903CE3F4CEB8BB0C447B0A57B5F29E2A9` |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` | `3BBA33EF71E44FE08F9911F2B203026DE6ED8AE1739AE648A07BF4741B03523E` | `580A5F0CB1917C5443B5255464CA429E491A1C79C9C0CEEF6121A7B8D89C598D` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentExecutionPreparationCacheTests.cs` | `ABSENT` | `186BD2C70F33B530DED2F877F95723A7D9D8F699008AF09062E287C8FA3A1DA3` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentExecutionPreparationServiceTests.cs` | `ABSENT` | `51032A4C19E96EAE59F2E30BFDFF69FB5ACC78D70D25A9F5EC8FB4E0F919D826` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/ProviderRuntimeProfileSnapshotServiceTests.cs` | `ABSENT` | `652C591705902CEBBEA9C0ED7C948C9263920A42AC9D28FAC6E7E9A2BB886AA5` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/DatabaseRuntimeAgentExecutionProfileGenerationSourceTests.cs` | `ABSENT` | `78BB104391110D7632F91B47B19B4BC4334AC33FA3832B50772D06E36FAA910C` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentReferenceDataProviderTests.cs` | `D594ACDFF4E7832189827DC1E76228F9984DD426832EF509E4AFAE53A2B216E7` | `AA97D2E2D4F8A708253AF64AE16237C1FD434F627AD09A0452EE21D48E73CCED` |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs` | `A275729F3870B34C520B5672F390CC66BD74E059CA9CC79B419D115355F915E4` | `C8D6573D917FF20FB7073602676A7AFCF832201013F7DA083B6E411296726C94` |

## Residual P2s and scope

- A blocked synchronous database-switch subscriber can delay the switching thread.
- Final provider validation cannot be globally atomic across hosts without a
  distributed lease/version boundary.
- Skill parsing, tool/runtime-session construction, credentials, current
  authorization, and context contribution remain per dispatch.
- SB05 separately retains its physical WAL/directory durability P2.

## Closure statement

The integrated execution path now obtains a provider through one canonical, immutable,
profile-generation-bound snapshot. Warm execution performs no provider-registry or
full-provider-profile reload. For a non-synthetic unchanged provider, SB05's later SQL
instrumentation records one scalar canonical revision probe; synthetic is zero and a
changed provider is three commands. That later 1/0/3 evidence supersedes the earlier
SB03 interpretation that all capture operations were database-free. The immutable
provider-state lookup itself is lock-free; current database-profile identity uses the
existing short in-memory `DatabaseRuntimeState` lock.

The four-case SB03 diagnostic remains an operation/order artifact rather than a
statistically valid latency benchmark. SB05 owns the performance decision and returned
`GO with three P2 follow-ups`. The earlier parallel-SB04 compile failure remains
preserved as historical evidence, not current state; later solution/build,
architecture, unit, component, and A5/A6 evidence superseded that transient blocker.
A3 is `PASS with two A3 P2 follow-ups`, and SB04 progression is authorized.
