# 03 – Source map: soubory a místa, která musí Codex opravit

Tato mapa uvádí aktuální kódová místa, která se podílela na incidentu nebo na zbytečné eskalaci. Řádkování odpovídá přiloženému archivu `CanDoItAll-memory-providers(1).zip`.

## Adapter result conversion

### `AgentFrameworkProcessExecutionAdapter.ResultConversion.cs:31-205`

Důležité místo:

- `Completed` se nejdřív mapuje na `StrategyOutcome.Succeeded`.
- Pro `Succeeded` se spouští řetězec validací.
- `ValidateProductMutationCompletion` je na řádcích 155–158 před `ValidateRequiredProductToolReceipts` na řádcích 167–170.
- První nalezený issue okamžitě vrátí `NeedsManagerForCompletionIssue`.

Problém:

- validace short-circuituje,
- v incidentu se vrátí `product_required_file_content_missing`, ale nezachytí se zároveň missing `workspace_pwsh_run_script`,
- výsledek je rovnou `NeedsManager`, ne typed safe retry.

Codex úkol:

- nahradit řetěz `if (...) return NeedsManagerForCompletionIssue(...)` agregovaným `IProcessCompletionGateEvaluator`,
- zachovat backward-compatible primary issue, ale přidat secondary diagnostics,
- nenechat safe/idempotent completion gate failure automaticky znamenat manager escalation.

## Product completion paths

### `AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs:30-70`

Důležité místo:

- `ValidateProductMutationCompletion` spouští product root/path/file-content kontroly.
- Řádky 57–64 volají `ValidateRequiredProductPaths` a `ValidateRequiredProductFileContentChecks`.

### `AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs:190-235`

Důležité místo:

- Řádky 199–202 kontrolují, zda soubor obsahuje požadovaný text.
- Řádky 228–235 vytvářejí issue `process.adapter.product_required_file_content_missing` se `SafeToRetry` + `Idempotent`.

Problém:

- tato validace je správná, ale jako první issue schová missing tool receipt.
- kód vrací jeden `ProcessCompletionIssue`, ne sadu gate failures.

Codex úkol:

- ponechat file-content gate,
- předělat na `IProductReadbackGate` vracející sadu issues,
- zahrnout resolved path, expected text groups a skutečný readback excerpt/length/hash do evidence payloadu.

## Product completion receipts

### `AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs:61-95`

Důležité místo:

- `ValidateRequiredProductToolReceipts` vyhodnocuje `ProductCompletionRequiredToolReceipts`.
- Kdyby se dostala ke slovu, v incidentu by našla chybějící `workspace_pwsh_run_script`.

### `AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs:246-274`

Důležité místo:

- `BuildMissingRequiredToolReceiptGuidance` skládá retry guidance pro missing `workspace_pwsh_run_script`.
- Používá `assignment.LaunchVariables[scriptRefVariableName]` přímo.

Problém:

- guidance by v tomto incidentu použila unresolved path `artifacts/process-runs/{CurrentProcessRunId}/scripts/...`,
- tedy i případný rework by mohl vést agenta k chybné path.

Codex úkol:

- guidance musí pracovat s resolved launch variables,
- missing receipt guidance musí být součástí aggregate issue,
- přidat matcher podle tool name + expected args, nejen name/pattern.

## Completion issue conversion

### `AgentFrameworkProcessExecutionAdapter.CompletionIssueResults.cs:30-65`

Důležité místo:

- `NeedsManagerForCompletionIssue` vždy vytvoří `ProcessExecutionAdapterResult` se `StrategyOutcome.NeedsManager`.

Problém:

- method name i návratový typ svádí k tomu, že každý completion gate issue znamená manager.
- `ProcessCompletionIssue` sice nese retry safety/idempotency, ale tato informace se v této vrstvě nepoužije k odlišení safe retry.

Codex úkol:

- přejmenovat/rozšířit na neutrální `CreateCompletionGateResult`,
- výsledek musí zachovat issue retry metadata,
- recovery rozhodnutí nesmí být hardcoded manager.

## Runtime recovery decision

### `ProcessRuntimeEngine.ResultHelpers.cs:203-228`

Důležité místo:

- Pro každý `Blocked` step se vrací `ProcessRecoveryDecisionKind.ManagerRequired`.

Problém:

- ignoruje `retrySafety` a `idempotency` z diagnostik,
- ignoruje existující enum hodnotu `SafeRetry`.

### `ProcessRuntimeEngine.ResultHelpers.cs:250-280`

Důležité místo:

- `ResolveRecoveryRouteKind` routuje MissingArtifact, ChildRunBlocked, PolicyViolation, jinak ManagerAction.

Problém:

- neexistuje route pro product completion gate / current step safe retry.

### `ProcessRuntimeEngine.ResultHelpers.cs:306-373`

Důležité místo:

- `ClassifyFailureCategory` používá substring heuristiky.
- `process.adapter.product_required_file_content_missing` skončí jako `Unknown`.

Codex úkol:

- přidat explicitní classification pro `product_required_*`, `process_required_*`, `completion_gates_unsatisfied`, `required_tool_receipt_missing`, `product_required_state_*`,
- použít diagnostická metadata místo substring-only heuristik,
- pro safe/idempotent completion gates použít `SafeRetry` + `CurrentStepRetry`.

## Recovery enums existují, ale nepoužívají se

### `ProcessRuntimeState.cs:194-210`

Důležité místo:

- `ProcessRecoveryDecisionKind.SafeRetry` existuje.
- `ProcessRecoveryRouteKind.CurrentStepRetry` existuje.

Problém:

- design již počítal se safe retry, ale engine jej nepoužívá pro Blocked status.

Codex úkol:

- přidat unit test, který dnes selže: safe/idempotent diagnostic nesmí vést na `ManagerRequired`.

## Dispatch / manager recovery instruction

### `ProcessRuntimeDispatchApplicationService.cs:338-345`

Důležité místo:

- Po `NeedsManager` se aplikuje manager recovery instruction.

### `ProcessRuntimeDispatchApplicationService.cs:960-1013`

Důležité místo:

- `ApplyManagerRecoveryInstructionAsync` skládá obecnou manager instrukci.

Problém:

- žádná automatická current-step rework cesta,
- žádná diagnosticky specifická instrukce.

Codex úkol:

- přidat `TryApplyAutomaticSafeReworkAsync`,
- volat jej před manager escalation, pokud recovery decision je `SafeRetry/CurrentStepRetry`,
- použít `IProcessStepRecoveryInstructionBuilder`.

## Operator rework

### `ProcessRuntimeOperatorApplicationService.cs:190-282`

Důležité místo:

- Ruční `RequestStepReworkAsync` appenduje obecný důvod.
- Repair service primárně řeší assignment readiness.

Problém:

- pokud agent readiness projde, rework nedostane konkrétní opravu missing tool/readback gate.

Codex úkol:

- i manuální rework musí přidat poslední diagnostic-specific repair packet,
- operator UI může ukázat „auto repair plan“ před potvrzením.

## Launch variables enrichment

### `ProcessLaunchApplicationService.cs:1119-1240`

Důležité místo:

- `EnrichRunLaunchVariables` přidává `CurrentProcessRunId`, `CurrentManagedArtifactRoot` atd.
- Step launch variables se také obohacují.

Problém:

- kód nepřepisuje placeholdery ve values, které už existují.
- `DotNetCreateProjectScriptRef` zůstane `artifacts/process-runs/{CurrentProcessRunId}/scripts/...`.

Codex úkol:

- přidat `ILaunchVariableTemplateResolver`,
- spustit po run-level i step-level enrich,
- pro tool-critical unresolved placeholdery failnout template/launch validation před agentem.

## Brief contracts

### `ProcessStepBriefContracts.cs:26-125`

Důležité místo:

- `GenericProcessStepBriefBuilder` vypisuje launch variables do promptu.
- Hodnoty se zkracují až nad 2400 znaků.

Pozorování:

- `DotNetCreateProjectExecutionPlan` v incidentu měl cca 1963 znaků a nebyl zkrácen.
- `DotNetCreateProjectScript` měl cca 2365 znaků a nebyl zkrácen.

Závěr:

- agent plán viděl, ale nedodržel ho.
- problém není primárně v truncation, ale v prompt-only orchestration.

## Parent subprocess bridge

### `ParentSubprocessArtifactBridge.cs:65-145`

Důležité místo:

- Pokud child není active a není completed, kód na řádcích 116–118 udělá `continue`.

Problém:

- stopped Blocked child není vrácen jako konkrétní bridge result s child diagnostikou,
- parent pak propadne do generic blocked propagation.

### `ParentSubprocessArtifactBridge.cs:310-337`

Důležité místo:

- `TryResolveChildOutputRefs` hledá fyzické soubory pod `artifacts/process-runs/{child}/steps/{step}.md`.

Problém:

- file existence není totéž jako runtime-accepted artifact slot.
- V incidentu fyzický artifact existoval, ale `ProducedArtifactsJson` bylo prázdné.

Codex úkol:

- bridge má číst child runtime state/receipt/ledger,
- pro blocked child má vracet `ChildStoppedBlocked` s child diagnostic payloadem,
- accepted/no-go bridge má preferovat ledger/slot IDs.

## Hardcoded subprocess resolver

### `ProcessSubprocessContractResolver.cs`

Důležité místo:

- Resolver obsahuje static mapování parent step → subprocess definition a accepted/no-go child outputs.

Problém:

- contract je v kódu, ne v template schema,
- template a runtime mapping se mohou rozejít,
- validace template nemůže odhalit nekompatibilní child output contract.

Codex úkol:

- přesunout subprocess bridge contract do template JSON schema,
- resolver ponechat jen jako backwards-compatible fallback s warningem,
- přidat template validation test.

## Tool preflight

### `ProcessRuntimeToolPreflightService.cs:54-115`

Důležité místo:

- Preflight porovnává required tool names se složenými tool names.

Problém:

- neověřuje exact args/path/workingDirectory/manifest/scope,
- tool může existovat, ale konkrétní invocation by byla denied nebo by používala unresolved placeholder.

Codex úkol:

- doplnit argument-level preflight pro typed plan items,
- preflight result musí umět říct `TemplateRepair` vs `CurrentStepRetry` vs `ManagerRequired`.

## .NET launch variable contributor

### `ProjectStructureProcessLaunchVariableContributor.cs:145-195`

Důležité místo:

- Vkládá `DotNetCreateProjectScriptRef` jako `artifacts/process-runs/{CurrentProcessRunId}/scripts/...`.

### `ProjectStructureProcessLaunchVariableContributor.cs:517-537`

Důležité místo:

- `BuildSolutionSetupCompletionRequiredToolReceiptMap` vyžaduje pro `create-dotnet-project`: `template=sln`, app template, `workspace_pwsh_run_script`.

### `ProjectStructureProcessLaunchVariableContributor.cs:812-837`

Důležité místo:

- `BuildCreateProjectExecutionPlan` obsahuje správný plán v textu.

### `ProjectStructureProcessLaunchVariableContributor.cs:859-920`

Důležité místo:

- `BuildCreateProjectScript` obsahuje správný helper, který provede `dotnet sln add` a ověří membership.

Závěr:

- Template/contributor ví, co se má stát.
- Selhání je v tom, že plán je text a placeholder path není resolved.

## Process prompt builders

### `ProcessStepContractPromptBuilder.cs:86-115`

Důležité místo:

- Prompt říká, že required runtime tool musí mít current execution-run receipt.

### `AgentFrameworkProcessStepBriefBuilder.cs:58-75`

Důležité místo:

- Prompt říká, že je to tool-backed process step a finalizer až po required evidence.

### `AgentFrameworkProcessStepBriefBuilder.cs:369-415`

Důležité místo:

- Obsahuje guidance pro subprocess kroky včetně runtime-owned launch mode.

Závěr:

- Prompt pravidla existují, ale nejsou dostatečná kontrolní vrstva.
- Codex nemá dále přidávat další dlouhou prose instrukci jako hlavní fix. Je nutné typed/runtime enforcement.

## Managed artifact materialization

### `AgentFrameworkProcessExecutionAdapter.cs:229-293`

Důležité místo:

- Adapter nejdřív materializuje/appenduje managed outcome artifact a až potom volá `ToAdapterResult`, kde proběhnou product completion gates.

### `AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs:526-537`

Důležité místo:

- Appendix se jmenuje `Runtime Validated Structured Outcome`.

Problém:

- V incidentu se do artifactu appendnul text, že runtime validoval structured outcome, ale pozdější product gate ho odmítla. Artifact tak obsahuje zavádějící `Status: Completed` a „Runtime Validated“ sekci, přestože receipt artifact nepřijal.

Codex úkol:

- rozlišit `StructuredOutcomeValidated` vs `CompletionGatesAccepted`,
- appendovat „accepted“ appendix až po všech gates,
- u odmítnutých completion gates appendovat volitelně `Runtime Rejected Structured Outcome` s diagnostic, nebo používat staging artifact.

## MAF finalizer

### `MafAgentRuntime.cs:552-666`

Důležité místo:

- MAF řeší chybějící required finalizer bounded repair turnem.
- Validuje required finalizer a structured output contract.

Problém:

- MAF finalizer guard nezná process semantic gates: required tool receipts, product readback, artifact slots.

Codex úkol:

- nepřetěžovat MAF obecnou process logikou, ale přidat process-facing semantic guard po finalizeru a před acceptance,
- nebo předat typed gate result do Process adapter tak, aby safe/idempotent failure šel do auto-rework.
