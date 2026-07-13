# Source map and fix targets

## Runtime adapter

| File | Lines | Finding | Required change |
|---|---:|---|---|
| `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.CompletionGates.cs` | 30-47 | Completion gates jsou volané sekvenčně bez branch/purpose contextu. | Zavést `ProcessCompletionGateContext` s `StepKey`, `BranchOutcomeKey`, `currentExecutionRunId`, branch metadata, active rule set a issue routerem. |
| same | 71-99 | Priorita receipt missing před content failure způsobuje, že acceptance-only proof gate může zastínit branch-routable defect. | Ordering má pracovat s route kind: `Manager`, `BranchRoute`, `CurrentStepRetry`, ne jen s issue code. |
| `AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs` | 61-95 | Product required receipts jsou string list a nejsou branch-aware. | Přidat rule model s `EnforceBranchOutcomeKeys`, `SkipBranchOutcomeKeys`, `Purpose`, `RequireCurrentRun`, `RequireSuccessfulExit`. |
| same | 97-121 | Process/capability receipts jsou vynucené bez branch outcome. | Capability receipt gate musí mít applicability context nebo se completion receipts přesunou mimo capability scope. |
| same | 124-139 | Enforcement závisí na allowed operations, ne na branch. | Přidat branch-specific rule filtering před enforcement. |
| same | 566-572 | Active capability receipts jsou aktivované podle product receipt tool names. | Po zavedení structured rules musí parser číst tool names z object rules, ne jen string array. |
| `ProcessRequiredToolReceiptGate.cs` | 17-30 | Generic gate umí jen active launch tools, ne branch applicability. | Buď rozšířit `ProcessRequiredToolReceipt` o branch/purpose fields, nebo gate volat jen s už přefiltrovanými receipts. |
| `AgentFrameworkProcessExecutionAdapter.ResultConversion.cs` | 149-159 | Při completion issue se vrátí `NeedsManager` před vytvořením branch signals. | Vložit `ProcessCompletionIssueRouter` před `NeedsManagerForCompletionIssues`. |
| same | 184-191 | Branch signal vzniká až po satisfied completion gates. | Branch-routed completion issue musí umět vytvořit `Succeeded` result s branch signalem a runtime gate evidence. |
| `AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs` | 163-218 | File content checks už umí `EnforceBranchOutcomeKeys`, ale failure na acceptance branch jde do SafeRetry. | Pokud check selže na accepted branch a template má failure route, issue má být branch-routable. |
| `AgentFrameworkProcessExecutionAdapter.Types.cs` | 73-79 | `ProcessCompletionIssue` nemá routing metadata. | Přidat route kind / suggested branch / issue purpose / diagnostic source grouping. |

## Contracts and launch variables

| File | Lines | Finding | Required change |
|---|---:|---|---|
| `src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs` | 147-167 | `ProcessRequiredToolReceipt` nemá branch/purpose metadata. | Rozšířit nebo oddělit branch-aware completion receipt rule model. Zachovat backwards compatibility. |
| same | 208-223 | `FromProductCompletionRequiredToolReceipts(JsonElement)` čte jen string arrays. | Přidat parsing object rules. |
| `src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs` | 1195-1283 | Step-scoped launch variables se resolve na direct variables, ale bez structured rule awareness. | Zachovat JSON object arrays při step resolution. |
| same | 1529-1547 | `FormatProductCompletionRequiredStringList` zahazuje object rules. | Nahradit za normalizer, který podporuje string i object rule arrays. |

## DotNet / software-delivery contributor

| File | Lines | Finding | Required change |
|---|---:|---|---|
| `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs` | 547-562 | `qa-validation` a `qa-recheck` dostanou validation + browser receipts jako plain string array. | Emitovat structured branch-aware rules: browser proof jen pro `quality-accepted`; repair branch vyžaduje concrete defect evidence, ne acceptance proof. |
| same | 606-624 | Browser proof receipts jsou generované správně, ale bez účelu. | Označit purpose `AcceptanceUiProof` a branch applicability. |
| same | 627-659 | Scaffold file content checks jsou branch-gated pro acceptance a ungated pro repair. | Doplnit failure routing metadata pro acceptance failure -> repair branch. |
| same | 661-711 | Default Blazor scaffold checks jsou v .NET contributoru, což je správná doménová hranice. | Zachovat zde, nepřesouvat do generic process core. |

## Templates

| File | Lines | Finding | Required change |
|---|---:|---|---|
| `Templates/Processes/processes/software-delivery/definition.json` | 655-720 | `qa-validation.CapabilityScope.RequiredReceipts` obsahuje acceptance-only browser tools bez branch condition. | Buď odstranit z capability scope, nebo rozšířit o branch/purpose metadata. |
| same | 798-808 | Branch outcomes existují, ale nemají machine-readable failure routing. | Přidat metadata typu `CompletionIssueRoutes` nebo `BranchOutcomeSemantics`. |
| same | 945-1010 | Stejný problém u `qa-recheck`. | Browser proof jen pro `quality-accepted`, unresolved defect -> `repair-escalation`. |
| `steps/qa-validation.md` | 7-20 | Text je dlouhý a obsahuje správné části, ale evidence matrix není explicitní. | Přidat branch evidence matrix a zakázat „missing proof because QA skipped it => repair“. |
| `steps/qa-recheck.md` | 7-20 | Stejný problém pro recheck. | Přidat branch evidence matrix. |
| `steps/quality-repair.md` | 6+ | Repair instrukce musí odstranit scaffold a ověřit defect, ale nemá acceptance matrix vazbu. | Repair musí odkazovat na gate findings a acceptance criteria ids. |

## Recovery and domain boundaries

| File | Lines | Finding | Required change |
|---|---:|---|---|
| `src/Processes/CanDoItAll.Processes.Application/ProcessStepRecoveryInstructionBuilder.cs` | 40-60 | Generic application vrstva obsahuje .NET tools a software-delivery branch keys. | Zavést `IProcessRecoveryAdviceProvider`; přesunout .NET/software-delivery advice do workbench/template provideru. |
| same | 228-245 | Recovery prompt říká agentovi, aby zvolil repair branch, ale runtime ji potom stejně zablokuje. | Runtime musí branch route podporovat, prompt nemá suplovat chybějící systémové chování. |
| same | 397-445 | QA receipt guidance vynucuje browser tools i když jde o defect branch. | Guidance má vycházet z branch-aware rule setu. |

## Tests

| File | Lines | Finding | Required change |
|---|---:|---|---|
| `tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs` | 2297-2354 | Acceptance content failure test očekává `NeedsManager`. | Změnit/rozšířit: pokud existuje failure route, očekávat branch signal. Zachovat test bez route mappingu pro `NeedsManager`. |
| same | 4471-4510 | Product required receipts test používá string map. | Přidat structured rule object map + backwards compatibility test. |
| `DotNetProcessLaunchVariableContributorTests.cs` | 142-150 | Očekává `Dictionary<string,string[]>`. | Aktualizovat na rule arrays a branch applicability assertions. |
| `ProcessStepRecoveryInstructionBuilderTests.cs` | 125-175 | Testy potvrzují hardcoded QA/.NET recovery. | Přepsat na provider-based recovery advice a přidat domain boundary test. |
