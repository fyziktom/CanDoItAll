# H2 current-write source checkpoint

Status: source authored in the managed continuation and selectively integrated by the parent. Seven changed production projects and the Unit/Components/Integration owning assemblies built successfully in standard Release, and final portability enforcement passed. Exact discovery/execution is pending for this candidate. No H2 runtime or capacity result is claimed. The source review follows [the approved H2 card](../plan/P02b-H2-current-write-cutover.md).

## Implemented boundary

The actual JWT validation event creates an internal immutable observation after existing cryptographic/payload checks. Ordinary sessions select their validated identity and authority generation from one prepared full snapshot. Protected write capture consumes that same request feature; missing, ambiguous or substituted identity facts refuse without a full-read fallback. Existing fresh session resolution remains available to non-Web consumers and receipt disclosure. Unsupported observation shapes leave ordinary authentication behavior intact while refusing this protected capture path.

HTTP write invocations now require a typed current-write observer. Native local and real governed source invocations retain their own authority. The owner observes current authority after initial source/lifetime/operation waits and the replay branch, then again after its last final source/lifetime check immediately before recording the commit attempt. Both observations use only bounded current-token and account-generation reads. A one-second cooperative budget drains the awaited operation; it is not a hard OS-open deadline. Caller cancellation remains distinct. The trusted precommit decision survives cleanup aggregation; existing accepted/replayed precedence and generic uncertain-commit recovery are unchanged.

Web startup and the API fixture prepare the existing H1 stores before readiness. No store interface member, SQL migration, source lease, UI writer or SPA activation was added. HTTP 503 metadata distinguishes rejected authority observation from unresolved commit. The parent separately repairs the pre-existing emitted-schema/reference-description failures. The routed 415 metadata now uses the existing ApiErrorResponse actually emitted before the handler; media dispatch behavior is unchanged.

The protected capture is pure and performs no authority-file I/O before parsing. Therefore this implementation has no new pre-parse unavailable branch: missing evidence uses the existing denial path. The new typed unavailable 503 arises from the effect-time observations and includes the original operation. Existing pre-authentication store I/O still follows the existing 401 behavior.

## Source-derived inventory and pending proof

| New class | Assembly | Facts | Scope |
| --- | --- | ---: | --- |
| ApiSessionAuthoritySnapshotTests | Unit | 4 | Shared ordinary validation, prepared/fresh entrypoints, administrator binding, detached selected snapshot. |
| ProjectDefinitionCurrentWriteContractTests | Unit | 4 | Required caller-specific observer, closed refusal mapping, commit/cancellation boundary, preserved real governed source/effect contract. |
| ApiAuthorityFileReadLifecycleTests | Unit | 2 | Parent-authored actual production read handle with atomic publication, and truly pending canceled read drainage. |
| ProjectDefinitionCurrentWriteAccessTests | Integration | 8 | Actual JWT middleware/InMemory host provenance and one selected read; immutable claims and managed metadata; legacy/open mode; generation changes; cooperative budget/caller cancellation; actual HTTP result execution. |
| ProjectDefinitionHttpCurrentWriteTests | Integration | 12 | Actual PostgreSQL/HTTP lock-wait, final guard, rollback/cleanup and replay arrangements; independent source review found no blocking issue. See [the exact source inventory](../reviews/H2-http-current-write-source-proof.md). |

The first four classes contain **18 Facts**, no Theory/member provider and no inherited test methods. Source attributes were counted from these exact files without discovery or provider execution. The eight Integration cases use no database server; one runs the actual API fixture with the explicit InMemory database. Existing ProjectDefinitionOpenApiSchemaTests retains three Facts, with one existing 503 assertion extended; it is separate regression coverage, not three new H2 cases. The constructor inventory found one production authenticated/open invocation owner, ProjectDefinitionHttpAccess. Local and governed consumers keep their current caller kinds.

The contract unit cases do not directly execute the old legacy transaction commit path. The existing owner/source regression lane must establish that compatibility. The real middleware case covers successful provenance and duplicate scope representations; direct fixture cases cover feature absence, ambiguous subject/managed-version facts and post-capture substitution. They do not pretend manually constructed test observations are authenticated HTTP requests.

Expected focused filters before the sibling file freezes:

- Unit: FullyQualifiedName~ApiSessionAuthoritySnapshotTests|FullyQualifiedName~ProjectDefinitionCurrentWriteContractTests|FullyQualifiedName~ApiAuthorityFileReadLifecycleTests — **10**.
- Integration: FullyQualifiedName~ProjectDefinitionCurrentWriteAccessTests — **8**.
- Integration schema regression: FullyQualifiedName~ProjectDefinitionOpenApiSchemaTests — **3 existing**, awaiting the parent schema repair.

The authored new total is **30 Facts**, with no Theory/data provider/Skip. This is an independently checked source expectation, not a discovered or executed result. No measurement claim is made for the removed redundant scan. The approved 36-cell capacity matrix, raw maximum-byte input distinction, final portability gate and actual Windows publication proof remain required.

## Exact checkpoint inputs

These SHA-256 values identify the reviewed source checkpoint, not a built candidate. The parent owns ApiAuthorityFileRead.cs and its two-Fact lifecycle file. The schema helper and existing schema test are temporarily held for the parent's independent emitted-schema repair; their hashes below are provisional until that merge. All other H2 production and small-test source is frozen for parent review.

| Relative managed-checkout path | SHA-256 |
| --- | --- |
| `src/Modules/CanDoItAll.Modules.Projects/ProjectDefinitionCurrentWriteAccess.cs` | `2C140CB177E322FBB6D69A1FADE41BBD0B90BFD4FB2D617E7AC11D732279F4B8` |
| `src/Modules/CanDoItAll.Modules.Projects/ProjectDefinitionInvocation.cs` | `9136379F1EBB0E8161C7EB3DF70A846AA167D4C700C0473F5B583CE12BC1AD46` |
| `src/Modules/CanDoItAll.Modules.Projects/ProjectDefinitionOperationProtocol.cs` | `8B6BE64386F6435F4EDF788573499C88E6B698DDB4D8806E0E8462D9695F18E2` |
| `src/Modules/CanDoItAll.Modules.Projects/ProjectMutationAuthority.cs` | `0CBA823D1DE6F0813070B03CB7DF80BAB4B72713191A96265AEB2F5083D0B21B` |
| `src/Modules/CanDoItAll.Modules.Projects/ProjectDefinitionWriter.cs` | `803747DBF209982BE09AE82650466E59526C9DF660E7C8FCE3F0949F07CCDF60` |
| `src/Modules/CanDoItAll.Modules.Projects.Contracts/ProjectPortfolioModels.cs` | `8AB584F3821C0397A7E08D543B15AB3E5724DF52EA1EB4F05155267217FE89BC` |
| `src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiSessionAuthoritySnapshot.cs` | `A656FEF87719BCC8616AD3852F05466A93E78C1DB11F8B0D07862C78693FD52B` |
| `src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiSessionService.cs` | `8891F2F4796E159C024B9251B684C6740D8B116197C768E669B47463F3C1EFBE` |
| `src/App/CanDoItAll.Web/Api/ApiAuthenticationObservation.cs` | `804A5602D68DD51CC0BD153F0BF0EDAAC911B6A92638B4ED0D9316FD377B869C` |
| `src/App/CanDoItAll.Web/Api/ApiManagedTokenValidation.cs` | `19A0529D5F585876ED0814DFF37BD23F6E81DFDA94D21BFC5571D4EC7E3AE3D8` |
| `src/App/CanDoItAll.Web/Api/ProjectDefinitionCurrentWriteAccess.cs` | `087F2DEE7728CC526EE0A1096933E2FA8250F931BE54CAFA7ADCDB93E76BC12D` |
| `src/App/CanDoItAll.Web/Api/ApiAuthorityReadiness.cs` | `E708556DF510F6AC551D33E9B6D8AB252BE0076170CE9DB8269064211A473D82` |
| `src/App/CanDoItAll.Web/Api/WebCurrentPrincipalResolver.cs` | `915FBE89C3940365C5CFD9BE0250904F48D5CCE1EDFDA96B867DBF7E26EE823A` |
| `src/App/CanDoItAll.Web/Api/ProjectDefinitionHttpAccess.cs` | `B282584D66AB1BA85A38ACE0FF5A05586263CCB2ACAABA30E41CCB428A244E22` |
| `src/App/CanDoItAll.Web/Api/ProjectDefinitionApi.cs` | `417363E19230ECB1E4D685378AA6A1D6B65BC0BDF3DCE6FD124078AC2D21A08B` |
| `src/App/CanDoItAll.Web/Api/ProjectDefinitionOperationOpenApiSchemas.cs` | `C6C35BC32CCB7379F49A2C1403C37376749A394800A47010ADF91A983F28C186` |
| `src/App/CanDoItAll.Web/Api/ProjectDefinitionOpenApiOperations.cs` | `A67851DCDE21670F777086E6D1F2BE2A9F296DD1A78170BD3D5621594370AE07` |
| `src/App/CanDoItAll.Web/Program.cs` | `CEEF3219C49351E54BEEAC727F9E2740872A988F24AB11DC625E563EDBEFB12D` |
| `src/Foundation/CanDoItAll.Infrastructure/ControlPlane/ApiAuthorityFileRead.cs` | `89DCA2FF40D6FC17926524806F7BCB44326DFE650AEC9203AB8D8DC928B9459E` |
| `tests/Integration/CanDoItAll.Tests.Integration/ApiTestHost.cs` | `87C1D90B054B61DAC93A2E40A205CCA7471091E0AF1EAC70DEE2EE309CE154B8` |
| `tests/Integration/CanDoItAll.Tests.Integration/ProjectDefinitionOpenApiSchemaTests.cs` | `FE42B00503DE10AA96F02B6159195244EE72B887DBE744317D5396BAF4499D3C` |
| `tests/Integration/CanDoItAll.Tests.Integration/ProjectDefinitionCurrentWriteAccessTests.cs` | `D8A91049BAD82F9FCDC688D557543B797B589D5813E66E3A96CAE86DA7665163` |
| `tests/Unit/CanDoItAll.Tests.Unit/ApiSessionAuthoritySnapshotTests.cs` | `67677E183389BD827F189ED154C9480A9AB90E670805989147EC54DE094606F6` |
| `tests/Unit/CanDoItAll.Tests.Unit/ProjectDefinitionCurrentWriteContractTests.cs` | `2EE4E76A413326887304EC91CE1ED947687DF7D546A8569B7A7FA42F872A8EBC` |
| `tests/Unit/CanDoItAll.Tests.Unit/ApiAuthorityFileReadLifecycleTests.cs` | `7C5B185B6409E2A7AEC680EF9986A3EE54183AFB421DD0B30082F163055ACCE9` |

The checkout is C:\Users\lucys\.codex\worktrees\concurrent-mutations-runtime\CanDoItAll. No H2 process, database, browser, container, temporary fixture or dependency installation was created by this authoring lane. The table does not include unrelated managed changes owned by other agents.

