# SB02 Governed Proof Manifest

## Identity

- Subbundle: `SB02`
- Status: `Completed — independent Governed A2 Pass`
- Owned requirements: R01-R04 and the SB02 portion of R11.
- Raw note owned: “Make agent chat visibly responsive before an execution run exists” by introducing a strongly typed, isolated, bounded activity stream that publishes before catalog/provider/session work, correlates the initial operation with a later run, stays separate from durable history, and remains suitable for an authorization-aware SSE projection later.
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`
- Production artifact matrix: `bundle://proof/SB02/producer-consumer-lifecycle.md`
- Initial independent review: `bundle://proof/SB02/a2-independent-review.md`
- Second independent review: `bundle://proof/SB02/a2-second-independent-review.md`
- Final independent review: `bundle://proof/SB02/a2-final-independent-review.md`
- Closure gate: `bundle://proof/SB02/a2-closure-gate.md`

## Required evidence status

| Evidence | Status | Artifact |
| --- | --- | --- |
| Historical failing-first `ExecutionUpdated` isolation | Pass — preserved SB01 red | `bundle://proof/SB01/transcripts/failing-first-execution-updated-isolation.txt` |
| First independent A2 adversarial review | Fail — six findings recorded, not overwritten | `bundle://proof/SB02/a2-independent-review.md` |
| Focused stream/lifecycle/admission unit suite | Pass — 52/52 | `bundle://proof/SB02/transcripts/passing-focused-unit-52.txt` |
| A2 lifecycle implementation-first red | Pass — 6/6 targeted cases failed before repair | `bundle://proof/SB02/transcripts/failing-first-a2-lifecycle-repair.txt` |
| Controlled shallow-mutant red/green proof | Pass — replay, capacity, context binding, and profile pinning mutants killed | `bundle://proof/SB02/transcripts/controlled-shallow-mutant-red-green.txt` |
| Final repaired semantic unit suite | Pass — 58/58 | `bundle://proof/SB02/transcripts/passing-a2-final-validation.txt` |
| Component/downstream compatibility suite | Pass — 65/65 | `bundle://proof/SB02/transcripts/passing-component-65.txt` |
| Persistence/event/runtime integration suite | Pass — 5/5 | `bundle://proof/SB02/transcripts/passing-integration-5.txt` |
| Continuation and targeted run integration suite | Pass — 3/3 | `bundle://proof/SB02/transcripts/passing-continuation-targeted-3.txt` |
| Critical-foundation downstream unit smoke | Pass — 403/403 | `bundle://proof/SB02/transcripts/passing-downstream-unit-403.txt` |
| Web affected-host build | Pass — 0 errors; 125 existing NU1903 warnings | `bundle://proof/SB02/transcripts/passing-web-build.txt` |
| Static bypass, constructor, and anti-stub audit | Pass — forbidden bypasses 0; five constructors explicitly wired; stubs 0 | `bundle://proof/SB02/transcripts/static-bypass-and-anti-stub.txt` |
| Production source assertions | Present | `bundle://proof/SB02/source-assertions.md` |
| Producer/consumer/lifecycle matrix | Present | `bundle://proof/SB02/producer-consumer-lifecycle.md` |
| Post-repair architecture snapshot | Present — refreshed scoped snapshot; independent interpretation still pending | `bundle://proof/SB02/architecture-snapshot.md` |
| Changed-file hashes | Pass — LF-normalized SHA-256 captured for every owned production and test file | Tables below |
| Second independent A2 re-review | Fail — four findings recorded and preserved | `bundle://proof/SB02/a2-second-independent-review.md` |
| Final independent A2 closure review | Pass — all original and second-review findings closed | `bundle://proof/SB02/a2-final-independent-review.md` |

## Command Transcript Citations

- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-a2-lifecycle-repair.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/passing-a2-final-validation.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/static-bypass-and-anti-stub.txt`

## Failing-first boundary

The legacy `ExecutionUpdated` isolation failure from SB01 remains preserved. The A2
lifecycle repair also preserves the actual six-case pre-repair failure for cold
current-profile admission and queued old-profile notifications. For greenfield
contracts whose original red output was not retained, the Governed proof uses explicit
controlled shallow mutants for replay-zero, all-active capacity, context binding, and
profile-pinned dispatch. Each mutant's LF-normalized hash, focused failing command,
restoration, passing command, and restored hash are durable. No command output is
invented, and this evidence still does not substitute for an independent A2 decision.

## Changed production-file manifest

Hash convention is the SB01 convention: SHA-256 over LF-normalized UTF-8 content so Git
`HEAD` and a Windows working tree are comparable. Source and test hashes were captured
after the final SB02 source state was frozen for independent review.
`ABSENT` means the file did not exist in `HEAD`.
These are SB02 A2 gate-time provenance hashes, not current-checkout claims after
downstream SB03-SB07 edits.

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/App/CanDoItAll.Web/Api/AgentsApi.cs` | 08FA1546CD4D1738D2446B399188FDC70CCEC92AE7EEC91AB1622F0498B10242 | 76AB9E77C0E334F4CC45D7A427B8CDCFE8EA779621ED319D109B3EAF83BBD45C |
| `repo://src/App/CanDoItAll.Web/Program.cs` | 56F82EBD290522BE9805FE1D3965A47271DED4F4F46BB159B3C692D48F720007 | 5D647DE0046C616CB485384BC9C8C4E1C623E5794821114BA62B7E7346D6B0AD |
| `repo://src/Foundation/CanDoItAll.SharedKernel/Streaming/PartitionedSequencedStream.cs` | ABSENT | EC80F67D88FD0CFC67C3CF726CF7500826B3B595C8501E62674698324F892F76 |
| `repo://src/Foundation/CanDoItAll.SharedKernel/Streaming/PartitionedSequencedStreamContracts.cs` | ABSENT | F16D55DAA8015327654DAAAEA7352CD243245E48D72CD5316ECEE87CE12D9142 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Components/ContextualAgentWorkspaceWindows.razor` | E1463D8C86676EB7A28FEEFBA39C230CFD698EE00D72100FF4AA47049C9193A6 | 82AB33581D9116BCB88D3FBF947FF5E3E45C907C317D8F405AB5C3923B9FF8A9 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj` | 1794EA689E792EB451622D6C8943234675DD0905E8A79650FEBABB6E6042FA14 | 09C069F6F14C65BA7BE105DA6CCBD1BE164B81B69F6B771CC0CA1603884F5ECA |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentChatContextInvocationFactory.cs` | 09AE101D1FA1B244998CB0E8B0FD188017679B69AC52E95605E674E6816B21CD | 8CFD40FC2D9DAADA7887B342DA6D1A1492D01B69F8C59F445B738FA9C7E2280E |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Contracts/Contracts.cs` | 65289F9BAA2570120DB2B0E95AD86228B2CFDFC97E9698D20EF2DD9F989E08D0 | FCE982144AA04FCA289BE02AE47A68F2826DF550055174F8AFC0067B9D9738A7 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Events/IsolatedCompatibilityEventDispatcher.cs` | ABSENT | 4CA442632D000903C3D10DB9C8C3901E487A8270832BA9024B0529B272E663CB |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentExecutionActivityCoordinator.cs` | ABSENT | 619C68FFD3D24EC4559112056CA46EFC4D3F54B78D714091D8A3E0B28E53A8C3 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentExecutionActivityTransitionRules.cs` | ABSENT | E190F2CDFC7F6F75F8C04BE85F72ACCCCF43F6D5B56499512FAE6A168FE1829E |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs` | A21C6C145679833194BD65484703C740223FCF2DD57A48752F65CFF32E5EF325 | AF069BA6C47EB2DDEB9A255FC23B68E8C1B1456AD84C20BB782B4CC788CDC7E0 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` | 3BBA33EF71E44FE08F9911F2B203026DE6ED8AE1739AE648A07BF4741B03523E | 6FC86BE811A3EC4A62F48FF42EDC7D1B73C43726196ECAB2AC87187895B9C852 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs` | 514273DAE33156C4BA44CB7E2C7F6F80A08D6D5207E5340783E39250A56551C9 | D8E633A64A723BB825556DBC4C391833D43221BAE66D5CCADB4E9C4A89B31E58 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.cs` | 90C4348B5031DDF1ADB2B4F21B3A2F3427EAEBD4E7582E4FAFE6DCDD2693049E | 86B4EE76722F503E0656548884B9776619DADAF9F8718C42B6C162330F5E975B |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentFrameworkWorkspaceActivityExecutionContracts.cs` | ABSENT | FDD6481CEB67D203140E6BFFFB5A8C2AF8E0D31A4E2B8F0E55EDB839AAD1DADA |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentFrameworkWorkspaceService.ExecutionFacade.cs` | 739100F65FF76107C002D924EEE6D0444B39A59E3DCA67C23D916FD55656E014 | 52C2FC56EF1E6B50A71763A7F3F6DC49CF4D4DBE5C3CCC589596DEFE82D75332 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentFrameworkWorkspaceService.cs` | 34317E47FAB644EE01D48FEF354860E8682DFB9A9C89F4FED99C5C76D2AE6A16 | 42CC3157EAB13828F2261E9244C58631CC94D082DF41CE9E90F8A3629931B3F3 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/FloatingAgentChatContracts.cs` | 53F8AEF48678AC6565D224462F66FEAA8DC3B4ADDDAB2890246B304EC877D890 | 1327673EC066C6599AAC2D10497A99CABB46199E734CC9DE9001C400CBB775CA |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs` | BA843246AF42044083F7244644D809BB056EEE14554D7598FA2C32EFE85EBD70 | EC05A5AD8DA7B314813CEB7E2FC73DB527EF1E2184D71072DEE8ECA46596ED2C |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Conversations/AgentExecutionActivityModels.cs` | ABSENT | 5669ECFFFC99B1BE0FD50EE2783B340686B7253BAC476C3A38D1F2CC5DDEAF17 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs` | 82A0320BA068F3ED387FE496D68064DF05CD4F37DC9DA97DAEA5BC3D4AD7B46C | 337BEB269C5615D846BEDBCD1DB94103927C2C17CE242D2ECB0C9631AF3506A6 |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Hosting/ScenarioHarnessSupport.cs` | 5E71BC8C1962032F72DAA69E80C28AA874DD92203E8A07D6E63E7CBE21887F94 | 2B7DA906FFF5F3D3CEF63777FF09F10275F3B62F70F715D558587E9D6023CB9A |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentChatExecutionOrchestrator.cs` | 624C994FE5993ED390193B9DF2BE558BD7F707B4AD447C3D8161387499C95A5C | 00AB86C99D9D956ABE0CBF53912D8E7D1C7BE319D2FE1D0DA0D653F466E56879 |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs` | 0107FFB8CE2A4E3295FCFF648AC1B1FC7A82CC092906D585931F2DDBC07EE0B8 | 4447A1831C3630BF3B2CA511BBB81F6AF0F01C2227D079E20F1BA9AAB2B78311 |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/CurrentProfileAgentExecutionActivityReader.cs` | ABSENT | 4528B1FD0B0B2F39674861E9ED0F6604779E39E33C0140E0D71EB07B6F9F23C8 |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/Hr/HrAgentProcessReviewService.cs` | C35AFB8A4DC4D07FB8565BE8A5E0729A929B488CF2EA04A3C4A741030663C040 | 87E837B7DA7DDD4E0933786A13AF62B463FF2FC9A52B60E3DC40CB1EEB1535AF |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/AgentFrameworkWorkspaceFactory.cs` | C06919C6FA15C5350DFD6BFA4E16064098736808462BCB024D93DF9C0A990AF7 | 7571BEFB49DB65B7D2A5B59E3A91C0D3A94CCF4EC9B442107DE6089F01ED2FF1 |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs` | B23B26AFA9A69E3ECC9679C603D702E049C92E36755CF2B37AE2ECED877A59B1 | 5C082742CEB242DEEE588537BDBDAFAB870AF9D3CE1BF288F4640C18C9818BA0 |
| `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` | 6CC853E5264C0AC1D8932153AD14AF9C585AC3B60B3AD25A43F15EED4064DB57 | 2FAD7A27FCB9C264204EA0CBE14FB78066B9D31ABC5F940F03C347553B8C5BBC |
| `repo://src/Modules/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` | 7599FE3B7B04E6B858CBAB43DBE411DDC4140D5F5045A1052540DC294A151C20 | B3079EBCD0482559681BDA6A2C394500D639FB68AFF6CF2BC0F920D738B3B816 |
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessRunNarrativeGenerator.cs` | EC12E33BEE817DA0906988E4CAF7D1D809544BFA4B1CDA94B470553053EE710C | EC086714AE9D36219A957C1297DDFA5FD5FB7CE9ABBBC550CC1A53C5208A8002 |
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepExecutor.cs` | 6D585CA9A4CF7E07BB36BE190F3289200FEB86E8C2D10DC7EF0A9412B6B4C323 | 1B43D7ED0BD77C1A37113B856B6817D8BBE1FA52EAB6CD0AD6C06E27C271D704 |

## Changed test-file manifest

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://tests/Components/CanDoItAll.Tests.Components/AgentChatPanelResponsivenessTests.cs` | FE671C7737CD45CE29D033E181E16BF49B30D899EE497DA0A5BBA4E3FEB73F7B | 36EC11A7477F4ECA853E5A9F8EF356C654FB398DAF1F6AA06982A296B213CEF6 |
| `repo://tests/Components/CanDoItAll.Tests.Components/AgentFrameworkWorkspaceFactoryDisposalTests.cs` | ABSENT | 1F1FDAAD646E9EB870B6EC804B88FDA98F0E53C73BD87842888DDAB3450D41B7 |
| `repo://tests/Components/CanDoItAll.Tests.Components/HrAgentProcessReviewServiceTests.cs` | FFCDA93FE5271337076A8BB738E1C8C52EEA2F8D84A3939C27B05FEBDC03DEF1 | 91403DEC6E1F741DD31E636DB7BD35679FCF17E88B0EC05F5A867EDCEDCB0A1A |
| `repo://tests/Components/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs` | D7B28866A38E2BEECC5E5E26D74C6DF96289E86800CD79B9255AD059DD2347FE | A8087FDD82324D625FF335C234E7DE0B1663346AC87D99B10F397761995FB03B |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs` | A275729F3870B34C520B5672F390CC66BD74E059CA9CC79B419D115355F915E4 | 06421D29D8B0A4461F31892539BD1EEBE6B755BA69EBD321A0584C9BB8B5BD1F |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentJsonSchemaOutputExecutionIntegrationTests.cs` | 3A592A17BD9A6BEF2F536168E1CF0DFA6A15488AB3187BC7E43BB28B4D819BA4 | 3C4C5ED5D23B0F45CCD060089326806651FBBD7F3EFEDAD215F10E0B7DC4BE84 |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/LiveSpecialistAgentScenarioIntegrationTests.cs` | 00743961F92C01CCE7DD39FE655CBB9C880BFF808238C8B3D649550F6287F3B9 | 6B00F19C83D5B25F3E8339527819F23847D825DE5703B58016CBE8F7DDC71573 |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentChatExecutionActivityOrchestratorTests.cs` | ABSENT | 3FDC554CF11E74562F5001183005AC2121B69941CBD7AB1EFE77B406BFE573B4 |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentChatPositionTests.cs` | 127945214D59480DADDA71E0B2037082F7C581646DB370FBEB68EFAE0178E631 | C6AEA72D3F223B81CE6212349916C5AE1AAED423DDB303011ACA0863412AB4DC |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentExecutionActivityCoordinatorTests.cs` | ABSENT | EFDFCBF08EEA6CBDEEC993AF3D601077327D265371D4DFB6165FA80F72309B5C |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentExecutionActivityDependencyInjectionTests.cs` | ABSENT | 854A108EFFC31002EB89AE87A4ED98B40D27E977CB6C18DA8CCDE87462A25819 |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentExecutionOperationIdentityTests.cs` | ABSENT | 5655E5084D0760A4A11D792ABBDAB93360119EB692FF06F87957776A7CD2E5C0 |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs` | E30E8179BEA4D37FD7268B92C19724E5EF7E3BE344280A7EFB325D81C5625FD7 | 278CDF123E43FE6154791A71CC0A202F09A47ED423FC7EA5C46D8014D049DE31 |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFrameworkHostingServiceCollectionTests.cs` | 2F662AFB5500C7EEC29C6B49B85D47D831F46829F71315C9EBFDE539F9E24948 | CDFC42A7AAFF8FABA57BBC4F035B296623C609C230E23307094F360CA0BF0B5D |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFrameworkWorkspaceActivityAdmissionTests.cs` | ABSENT | 336DCF1865DAB06BB23E4F07CE75EB4E6F11629D0942218BB1F6B4CD49B82900 |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentReferenceDataProviderTests.cs` | D594ACDFF4E7832189827DC1E76228F9984DD426832EF509E4AFAE53A2B216E7 | 821F7F815F74465D6B80CFD26B853ED85DAEF172987DE62E61D00C859B0B629C |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` | 8BBB65FC001D1B89CD7607219A7BC9B0B5270A25E2EA8AC2BE5425EE1AD96422 | 677B61DAB0C73D2CAE17D7E858B65921675004E0D5281FEA07F7823315F79BC4 |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/CurrentProfileAgentExecutionActivityAdmissionTests.cs` | ABSENT | 961D60EFA3CE5EC1ECDA1E91DB43CD422A29815000BF6F3B3F7AD53C410549AF |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/CurrentProfileAgentFrameworkWorkspaceServiceTests.cs` | FFC0887DB1FE68EDA415E1191246CF36CE2BCCFE74E4C57D111D2AFE7C0A8F44 | F33F536F103AE596810F4AE4C21168D541F0ED8E766F0B4ADBDFEB4FAEF10CCB |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/ExecutionUpdatedCompatibilityIsolationTests.cs` | ABSENT | 2288B77AB466B4D4ECCDEDF9ED07EAC757E6B8649D5FA124D5A7C5E4A91B0BD6 |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/FloatingAgentChatArchitectureTests.cs` | 79706377EF268E7B8347A988AFCAD7F8F5B6AB45C54BEA3DD272AC012B73A2AF | 59DA69F18E85C5A5B88617AF77A91F746BB0F308042107FFCD84F96F534D891F |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/IsolatedCompatibilityEventDispatcherTests.cs` | ABSENT | F0EC20F0650D7D999EF0EF0529C8D6DBDBD50DAD9F245AF226F263FA6247C9E8 |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/PartitionedSequencedStreamTests.cs` | ABSENT | F2524BC36D33317E3FDAF873224B84C65E9286033724224824DE414806E07471 |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessLaunchExecutorResolverTests.cs` | 2C706F1DF650C729B521482DF077B0274887FA6F35B94FCB7F64089BD09EAFC2 | 8C89C9C107245AB8B01E7EA7E983694D68332E28CC91FA79B6FDB57165C5B4F0 |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRunNarrativeGeneratorTests.cs` | 436E629207A7AEC1EE1FA45DAE206BF1D5E2E4ABEBBCBB7B59752E66EA30D3DE | 706F8C89323317A02E2170A43356E4CA4384674F0B7348F0F63746C418F30295 |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs` | 5529E75F98907E661A7D1C608F1822A9426A17E61A2C933AA7A758DD9AEC8A88 | C3EAC5E6AC6F7DB7CA2089ABE4900F77B887C9D81A334BBB59C0055413FA90FC |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationMetadataTests.cs` | 3A35521A24C5A54B4BA5E936B9A2D741361153593C35A2E5168DF0451F3E75E2 | A9DD2DE64A91B69FDE9ED65F2C499DE98295A17F6BE19047C8A681A773ADB638 |

## Bundle-artifact manifest

The manifest omits its own hash because that would be self-referential.
Rows retain their A2 gate-time provenance unless this validator repair changed the
listed SB02 artifact. The semantic contract, transcript catalog, two CodeAnalytics
captures, and hash-verification transcript were recomputed after this grammar-only
repair; later edits to bundle-root or downstream-phase artifacts do not rewrite their
historical A2 hashes.

| Artifact | SHA-256 |
| --- | --- |
| `bundle://README.md` | EA30D0AD5C0BFD78B8AF95DA42B928FCBDC2F86FE40EFCA3A84BC3BC04B3DDD8 |
| `bundle://subbundles/02-02-typed-agent-activity-stream-foundation/README.md` | AF15333757B0A8FEEED0E2720B0D01FD7416E92664562EEA1592B109958BAED4 |
| `bundle://reviews/01-execution-report.md` | 63A5525730290DF8A8A96CB89E5673222D273E2CA840E338F36A53A1787F6E42 |
| `bundle://proof/SB02/a2-closure-gate.md` | E82B12876A3082453262707EF89B96B957F8DF7ED544644B3F1C1DAD25F4C5B3 |
| `bundle://proof/SB02/a2-final-independent-review.md` | 2E4326965CD32F37E345CE3FEB5238D65FD7AD4A1B6EC5363C93EDF1DB87C2C4 |
| `bundle://proof/SB02/a2-independent-review.md` | 60D0CB88CFF14E855FF5EC85D8C540EDD4188F7D66BA7749E7966531A1E95507 |
| `bundle://proof/SB02/a2-second-independent-review.md` | 137CA352493B4BA906F8A57B063EEB67AB9EEE9A59BC11F96F461801F30AD29A |
| `bundle://proof/SB02/architecture-snapshot.md` | 7C583506B6C26CACE21B56747E4B41D60C636FC4A13179CE382A6542977193D9 |
| `bundle://proof/SB02/producer-consumer-lifecycle.md` | 19994A6EDF1F4C1A6565F94FAF69597A0DA251558BD6766E68650748BA0D2B64 |
| `bundle://proof/SB02/semantic-invariants.md` | 2C2640DE55CE394425B80E5B02E87771759C718C9A278D67A8F9C361C57BA134 |
| `bundle://proof/SB02/source-assertions.md` | 3DD7DE293E868125ED0588C3540655CA61BCD6961BE28D7D12F9A80DDA88D436 |
| `bundle://proof/SB02/transcripts/README.md` | 3AA0A5A8327E9CFD8EA15CC22FD8AD9EFB43E96C2572A91BC37BE81AC0FBAC52 |
| `bundle://proof/SB02/transcripts/codeanalytics-post-change.txt` | BBB4306CD38D836F3D65B5DA4102908553409A5C25668EE463A94D7662FF6910 |
| `bundle://proof/SB02/transcripts/codeanalytics-post-repair.txt` | A0837354EE81A21C24A85D64C36240E2485FA21CD969F29EA4AF58FA46C0F866 |
| `bundle://proof/SB02/transcripts/controlled-shallow-mutant-red-green.txt` | FCE5B9627A887BEE80C7163F279E83495580EE71B5E08EEF1753B991BBBEE250 |
| `bundle://proof/SB02/transcripts/failing-first-a2-lifecycle-repair.txt` | 872243A6B980248E72FDE7B855E8E6BC2F9F0E4774062D9EC24CC15442CD7C9A |
| `bundle://proof/SB02/transcripts/hash-verification-post-repair.txt` | 91CD4C754D900652A0B07A3B775706D60BAFE9F73D9425F1DC83659DB3264C02 |
| `bundle://proof/SB02/transcripts/passing-a2-final-validation.txt` | 38914B3F2C3F1119BB11B192500448142098F730D3F8FF9070ED7F6A81D9B310 |
| `bundle://proof/SB02/transcripts/passing-a2-lifecycle-repair.txt` | 5152B3DD074BA7B30E43B5635F548E74C9ADA7834807FD2DE5F97D25307A01EE |
| `bundle://proof/SB02/transcripts/passing-component-65.txt` | C5E0D2A720C0F42EAF0D52888D25863EC2BF7031CB31E6A1A334D3321103D7A3 |
| `bundle://proof/SB02/transcripts/passing-continuation-targeted-3.txt` | C2BD161484A5C8BE6766CD6A8BB5DA8EB19F174024F0C716F04394C5F258A047 |
| `bundle://proof/SB02/transcripts/passing-downstream-unit-403.txt` | 8D070D1989C80AD06802363074A2E9BAAE09FFA663127B11011C79074B5C7FD1 |
| `bundle://proof/SB02/transcripts/passing-focused-unit-52.txt` | 71A9E529A40BD118BFB82B0D792B68F1391378B5189CB8BDC7FB3BDEBD3BCDE8 |
| `bundle://proof/SB02/transcripts/passing-integration-5.txt` | 10426AB2DC03A74656A238B72EE1463729DE189514216E48D3F9F98C1E76CE7D |
| `bundle://proof/SB02/transcripts/passing-web-build.txt` | 6B403B3B06F189B57CB85CC518ED45EDACE0F25F14C6708EC0956C570EFDEEF6 |
| `bundle://proof/SB02/transcripts/prepared-validator-post-repair.txt` | A4ADAC12190049C8F8B743A48CB8D8227241B94E11C3891F99A570577B7D7472 |
| `bundle://proof/SB02/transcripts/static-bypass-and-anti-stub.txt` | FB2CAD4E2069A895E8860E2F423F753987177B0772C1D07CCD327C2C6C51AA38 |

## Production Behavior Artifact Matrix

The detailed matrix is `bundle://proof/SB02/producer-consumer-lifecycle.md`.

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Sequenced typed activity | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentExecutionActivityCoordinator.cs`; `bundle://proof/SB02/source-assertions.md` | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/CurrentProfileAgentExecutionActivityReader.cs`; `bundle://proof/SB02/source-assertions.md` | `repo://src/Foundation/CanDoItAll.SharedKernel/Streaming/PartitionedSequencedStream.cs`; `bundle://proof/SB02/transcripts/passing-a2-final-validation.txt` | `bundle://proof/SB02/transcripts/controlled-shallow-mutant-red-green.txt`; `bundle://proof/SB02/transcripts/passing-focused-unit-52.txt` |
| Initial operation/run correlation | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentFrameworkWorkspaceService.ExecutionFacade.cs`; `bundle://proof/SB02/source-assertions.md` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`; `bundle://proof/SB02/transcripts/passing-continuation-targeted-3.txt` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Conversations/AgentExecutionActivityModels.cs`; `bundle://proof/SB02/transcripts/passing-integration-5.txt` | `bundle://proof/SB02/transcripts/failing-first-a2-lifecycle-repair.txt`; `bundle://proof/SB02/transcripts/passing-a2-lifecycle-repair.txt` |
| Context source/version | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentChatExecutionOrchestrator.cs`; `bundle://proof/SB02/source-assertions.md` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentExecutionActivityCoordinator.cs`; `bundle://proof/SB02/transcripts/passing-component-65.txt` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentChatContextInvocationFactory.cs`; `bundle://proof/SB02/transcripts/passing-focused-unit-52.txt` | `bundle://proof/SB02/transcripts/controlled-shallow-mutant-red-green.txt`; `bundle://proof/SB02/transcripts/passing-a2-final-validation.txt` |
| Compatibility `ExecutionUpdated` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs`; `bundle://proof/SB02/source-assertions.md` | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs`; `bundle://proof/SB02/producer-consumer-lifecycle.md` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Events/IsolatedCompatibilityEventDispatcher.cs`; `bundle://proof/SB02/transcripts/passing-integration-5.txt` | `bundle://proof/SB01/transcripts/failing-first-execution-updated-isolation.txt`; `bundle://proof/SB02/transcripts/passing-a2-final-validation.txt` |

## Closure

- First A2 independent decision: `Fail`.
- Second A2 independent decision: `Fail`.
- Final A2 independent decision: `Pass`.
- Current A2 closure: `Pass`.
- SB03 authorization: **Granted**.
