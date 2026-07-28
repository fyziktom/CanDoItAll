# SB01 Governed Proof Manifest

## Identity

- Subbundle: `SB01`
- Status: `Completed`
- Owned requirements: R09 baseline half, R11 architecture preservation.
- Raw notes: backend behavior must be deeply understood and measured before implementation; root causes, cross-threading/source-of-truth risks, DI/preload behavior, EF/file bottlenecks, and current UI-event behavior must be explicit.
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
- Entry gate: `bundle://proof/SB01/entry-gate.md`

## Existing preparation evidence

- Performance scan: `bundle://proof/SB01/performance-scan-baseline.md`
- EF query review: `bundle://proof/SB01/ef-query-review.md`
- Architecture gate: `bundle://reviews/csharp-architecture-gate.md`

## Required evidence status

| Evidence | Status | Artifact |
| --- | --- | --- |
| Failing-first adversarial transcript | Pass | `bundle://proof/SB01/transcripts/failing-first-execution-updated-isolation.txt` |
| Passing characterization transcript | Pass | `bundle://proof/SB01/transcripts/passing-startup-characterizations.txt` |
| Cold/warm new/existing-session baseline | Pass — 12/12 | `bundle://proof/SB01/startup-baseline.md` |
| Constructor/query inventory | Pass | `bundle://proof/SB01/constructor-query-inventory.md` |
| Deferred UI/cancellation/profile-relay contracts | Pass — explicit plans | `bundle://proof/SB01/deferred-characterization-contracts.md` |
| Architecture snapshot | Pass | `bundle://proof/SB01/architecture-snapshot.md` |
| Production source assertions | Pass | `bundle://proof/SB01/source-assertions.md` |
| Changed-file hashes | Pass | This manifest |
| Anti-stub audit | Pass | `bundle://proof/SB01/transcripts/anti-stub.txt` |
| Dependent-flow smoke | Pass — 17/17 | `bundle://proof/SB01/transcripts/dependent-flow-smoke.txt` |

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-execution-updated-isolation.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/passing-startup-characterizations.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub.txt`

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
| --- | --- | --- | --- | --- |
| `provider-backed` | `bundle://proof/SB01/source-assertions.md` identifies the real provider registry/runtime boundary and the injected `IAgentRuntime` replacement used to isolate it. | `bundle://proof/SB01/transcripts/passing-startup-baseline-matrix.txt` proves the deterministic startup matrix reaches runtime entry with all fail-closed provider methods untouched. | Adversarial negative fixture `bundle://proof/SB01/transcripts/anti-stub.txt` records that provider health, provider chat, and model-maintenance calls throw if the baseline crosses the provider boundary. | Verified boundary: SB01 is not provider-backed latency proof; no provider or model call occurred. |

## Changed-file manifest

No production source or skill file changed in SB01.
Hashes in this table are SHA-256 over LF-normalized UTF-8 content. This makes the Git `HEAD` content and the Windows working tree comparable without mixing blob and CRLF checkout conventions.

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs` | `A275729F3870B34C520B5672F390CC66BD74E059CA9CC79B419D115355F915E4` | `81C4D8F43188EE095CCF7993EA553A057D5C859EEABE2D9E06323287EEC2C5CC` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/FloatingAgentChatArchitectureTests.cs` | `79706377EF268E7B8347A988AFCAD7F8F5B6AB45C54BEA3DD272AC012B73A2AF` | `A6D20FE6AA089CA034504ED51B1A5C5CD3DF75ADC6C422C9E939F51110DC41DD` |

All bundle files are new initiative artifacts. This inventory covers every SB01 proof file plus the root/subbundle/status records touched during SB01. The manifest omits its own hash because that would be self-referential.

| Artifact | SHA-256 |
| --- | --- |
| `bundle://README.md` | `0800697431B03A3EB095CDC2134F146D97FC18F7CF629BD3477BFB738A00DDFA` |
| `bundle://reviews/01-execution-report.md` | `28E5CD15ACBE12C8BF583BC2998D874697169FF38D0C395C1838F7A55F9437AE` |
| `bundle://reviews/csharp-architecture-gate.md` | `A69BD6D3E9267C4C5685234DE00B553E876190DD4ADA219925684C28CB7B1881` |
| `bundle://subbundles/01-01-current-state-baseline-and-architecture-contracts/README.md` | `ECF0810AABCDFDCFD121FD7C2B9D8237DD7320E8C16DED3476C62719F8270C9E` |
| `bundle://proof/preparation/prepared-validator.txt` | `50167D29CA5F1337B361B809C07E1FE9844A9317C21C6F5FAA71A33DEA85715D` |
| `bundle://proof/SB01/a1-closure-gate.md` | `392A4013C1E77B85C0C78B35DED62C23CBA6C878C2FBBEE2C96BDFDB6F8C5912` |
| `bundle://proof/SB01/architecture-snapshot.md` | `775E5785D4E2D185F1ABE5D24437E588BAE2B77B1D9DC53B28665D5F405391C6` |
| `bundle://proof/SB01/constructor-query-inventory.md` | `62E136E729264375919423B27D23612CC3DC10B1A37E079FBBFDCFB0901F3E8D` |
| `bundle://proof/SB01/deferred-characterization-contracts.md` | `DB66CBF13CFFB42272F1CACB3C39A10CB1607EB3F955FF61809231470665F793` |
| `bundle://proof/SB01/ef-query-review.md` | `B487D1FA65AED9C4CBD58664CD00B89446D997D9E601B4F1377BCF8CE739E62D` |
| `bundle://proof/SB01/entry-gate.md` | `CB2E871C742CE0E1BAA125AF3216E045C3EDEEC7ACCAE0A6BBB136422A85EC41` |
| `bundle://proof/SB01/performance-scan-baseline.md` | `2D1C4159142CACC2C84D7B0B4FA1D44CFC18BCA4C7ADBFB9988121ED5E9EC923` |
| `bundle://proof/SB01/semantic-invariants.md` | `0F3F6CC7B3DDA9460200876475839C2B26A3E67CC9BB5A6F0D2009BBB01A8191` |
| `bundle://proof/SB01/source-assertions.md` | `B17A2AF60FC372C17EC9ED0D90F1BEBE03C7821DF9519991A443A40BB952103D` |
| `bundle://proof/SB01/startup-baseline.md` | `D16740806B6169C9B1F4A270F5B343A96BBFEDA8B8A713553C54C2F2B39EB1A2` |
| `bundle://proof/SB01/transcripts/anti-stub.txt` | `9E1CE6285DF38FF394A3E37451124ABE6EBE0D21981AA025A9447C0C885ADB73` |
| `bundle://proof/SB01/transcripts/codeanalytics-cycle-review.txt` | `B244BBF7E7FACF4C34D0BF98C4087B015D2005455C1C957C4AA4D201D39EE277` |
| `bundle://proof/SB01/transcripts/dependent-flow-smoke.txt` | `6DAECB29F4EBC17819669D769494990536EDA77417D11C332F24DD3F6BD5C9C2` |
| `bundle://proof/SB01/transcripts/failing-first-execution-updated-isolation.txt` | `A3CFEFBAD2871CDDE452346275A6F8D4452DF42E1E4E144540453A7D4C4F2CD2` |
| `bundle://proof/SB01/transcripts/manual-factory-wiring-source-assertion.txt` | `7DBAB7286B533412E9450800EB0050A5F92A61CC3136D63E9671DF0061B3552D` |
| `bundle://proof/SB01/transcripts/passing-preparation-single-flight.txt` | `462BD087721B47838BD688641EB9E82808E8DCC4FA1B441539A049DF575A4981` |
| `bundle://proof/SB01/transcripts/passing-startup-baseline-matrix.txt` | `FBFCE82102A1B04C261EE29DA10A570A2469F0A971D5081019F72770216B0522` |
| `bundle://proof/SB01/transcripts/passing-startup-characterizations.txt` | `5A68EC3C037D79CDD8B64B80FD11009D00EEF6EA17DFF272711815112727AB94` |
| `bundle://proof/SB01/transcripts/README.md` | `15C8CBD679DB63A8FC2225A4C2941A9B1CAC7564B5426CBA4636192CB2E1B7E6` |

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Current `ExecutionUpdated` planning entry | `source-assertions.md`; real store baseline | Workspace, relay, floating/panel/contextual consumers in `constructor-query-inventory.md` | `passing-startup-characterizations.txt`; `passing-startup-baseline-matrix.txt`; manual graph shares the same Core service per `manual-factory-wiring-source-assertion.txt` | Desired-behavior red plus final legacy characterization prove a throwing subscriber occurs after persistence and suppresses the sink/runtime |
| Current execution-event publication | `AgentFrameworkWorkspaceExecutionService.Helpers.cs`; recording sink | Real-app Null sink and generic-host buffered sink inventory | Always after `ExecutionUpdated` in all 12 baseline rows | Throwing subscriber prevents publication |
| Current runtime entry milestone | Deterministic barrier runtime after the real startup path | Baseline recorder | Ordered after Planning persistence, relay, and sink in all 12 rows | Throwing subscriber prevents entry |
| Current preparation metadata | Production `AgentChatPreparationPool` | Floating coordinator/open path | Same-agent concurrent acquire is single-flight | Warm preparation leaves all execution-path counts unchanged |

## Closure

- A1: `Pass` after independent Governed closure re-audit.
- SB02 authorization: Granted; A2 is the next blocking gate.
