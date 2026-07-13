# 02 – Root příčiny častých eskalací

Níže jsou root příčiny rozdělené podle vrstvy. Každá příčina obsahuje, proč se projevuje jako eskalace, proč navržená změna pomůže a jak ji otestovat.

## RC-1: Deterministická práce je stále řízená prompt-only způsobem

### Projev v incidentu

`create-dotnet-project` měl velmi konkrétní deterministický plán:

1. vytvořit/ověřit product root a `src`,
2. spustit `workspace_dotnet_new` pro solution,
3. spustit `workspace_dotnet_new` pro app,
4. zapsat helper PowerShell script,
5. ověřit helper,
6. spustit `workspace_pwsh_run_script`,
7. přečíst solution/app files,
8. teprve potom zapsat primary managed artifact.

Agent udělal jen kroky 1–3 a 8. Nejkritičtější kroky 4–7 přeskočil.

### Proč je to root příčina

LLM není dobrý executor deterministického orchestration plánu, pokud je plán jen dlouhá launch variable uvnitř velkého promptu. I když prompt obsahuje „mandatory“, agent může mylně vyhodnotit scaffold receipt jako dostatečný důkaz. U větších procesů se to bude opakovat: agent přeskočí přesný tool step, zamění důkaz za domněnku, nebo bude citovat upstream artifact místo current-run receiptu.

### Oprava

Přesunout deterministické části do runtime-owned plan executor vrstvy, alespoň pro infrastrukturní kroky jako:

- `create-dotnet-project`,
- `add-test-project`,
- `repair-solution-setup`,
- restore/build/test validation,
- browser/screenshot validation,
- project/media readback.

Minimální mezikrok: typed tool-plan guard, který před přijetím `Completed` ověří, že všechny kroky plánu mají current-run receipt, a při safe/idempotent failure vygeneruje cílený rework bez člověka.

### Proč to pomůže

Kritická pravidla přestanou být „text, který si model má pamatovat“. Runtime buď operaci provede sám, nebo bude mít explicitní stav plánu:

```csharp
public sealed record ProcessStepToolPlan(
    string PlanKey,
    IReadOnlyList<ProcessStepToolPlanItem> Items);

public sealed record ProcessStepToolPlanItem(
    string ItemKey,
    string RequiredToolName,
    IReadOnlyDictionary<string, string> ExpectedArguments,
    ProcessToolPlanItemRetryMode RetryMode);
```

Pak lze testovat, že chybějící `workspace_pwsh_run_script` nikdy neskončí jako vágní manager escalation.

## RC-2: Tool-critical launch variables obsahují nevyřešené placeholdery

### Projev v incidentu

Assignment obsahoval:

```text
DotNetCreateProjectScriptRef: artifacts/process-runs/{CurrentProcessRunId}/scripts/create-dotnet-project.wire-solution.ps1
```

Přitom stejný assignment zároveň obsahoval:

```text
CurrentProcessRunId: ab4a1ed8-8b1b-4974-973d-93983bf41f09
CurrentManagedArtifactRoot: artifacts/process-runs/ab4a1ed8-8b1b-4974-973d-93983bf41f09
```

Testy dnes tento unresolved placeholder dokonce očekávají:

- `DotNetProcessLaunchVariableContributorTests.cs:97-99`,
- `ProjectStructureAgentIntegrationTests.cs:1725-1727`.

### Proč je to root příčina

Runtime po agentovi chce, aby tool path byla přesná workspace-managed relative ref. Zároveň mu ale dává path s placeholderem. To zvyšuje šanci, že agent:

- path použije doslova,
- helper vůbec nezapíše,
- znejistí a vrátí blocker,
- nebo přeskočí helper a bude tvrdit, že scaffold receipt stačí.

U jiných procesů se stejný problém může projevit jako „agent nemá artefakt“, „nevím kam psát“, „tool denied“, protože tool path není skutečná grounded path.

### Oprava

Přidat `ILaunchVariableTemplateResolver` a spustit jej po obohacení run/step launch variables.

Pravidla:

- podporovat pouze známé placeholdery z aktuální launch-variable mapy,
- řešit `{Key}`, `${Key}` a případně `{{Key}}`, pokud se v repo používají,
- max 3–5 průchodů kvůli nested values,
- detekovat cykly,
- pro tool-critical keys zakázat unresolved placeholdery:
  - `*ScriptRef`,
  - `*ExecutionPlan`,
  - `*SideEffectManifest`,
  - `ProductCompletionRequired*`,
  - `RequiredRuntimeTool*`,
  - subprocess bridge refs,
  - managed artifact refs,
  - product path aliases.

### Proč to pomůže

Agent i recovery guidance budou pracovat s jednou konkrétní cestou:

```text
artifacts/process-runs/ab4a1ed8-8b1b-4974-973d-93983bf41f09/scripts/create-dotnet-project.wire-solution.ps1
```

Tím se sníží nejistota a zároveň se dají přesně porovnávat tool receipts proti očekávaným path argumentům.

## RC-3: Completion gate validace short-circuituje a ztrácí kompletní diagnózu

### Projev v incidentu

`ToAdapterResult` validuje úspěšný outcome v tomto pořadí:

1. `ValidateGroundedOutcomeReferences`,
2. `ValidateProductMutationCompletion`,
3. `ValidateProductMutationWriteReceipt`,
4. `ValidateRequiredProductToolReceipts`,
5. další validace.

`ValidateProductMutationCompletion` našla prázdnou solution a vrátila `product_required_file_content_missing`. Tím se `ValidateRequiredProductToolReceipts` už vůbec nespustila, takže se do receiptu nedostal stejně důležitý fakt: chyběl `workspace_pwsh_run_script`.

### Proč je to root příčina

Runtime sice zastaví nepravdivý `Completed`, ale operátor/agent dostane neúplný obraz. U větších procesů pak rework míří na první nalezený symptom, ne na celý chybějící tool plan. Pokud by agent dostal jen „solution neobsahuje csproj“, může znovu napsat artifact s tvrzením nebo spustit špatný repair, místo aby provedl povinný helper step.

### Oprava

Nahradit sekvenční `first issue wins` validace agregátorem:

```csharp
public interface IProcessCompletionGateEvaluator
{
    ProcessCompletionGateEvaluation EvaluateCompleted(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts,
        Guid? executionRunId);
}

public sealed record ProcessCompletionGateEvaluation(
    bool IsSatisfied,
    IReadOnlyList<ProcessCompletionIssue> Issues,
    ProcessCompletionIssue? PrimaryIssue);
```

Agregátor má vyhodnotit alespoň:

- grounded evidence refs,
- managed artifact write/materialization,
- required product paths,
- required file content/readback checks,
- required product tool receipts,
- required process tool receipts,
- required product state,
- declared blocker contradiction.

Primary issue může být prioritizovaný, ale receipt musí zachovat i secondary issues.

### Proč to pomůže

Rework instrukce uvidí celou příčinu:

```text
Missing current-run required tool receipt: workspace_pwsh_run_script.
Product readback failed: Calculator.slnx does not contain src/Calculator/Calculator.csproj.
```

To je výrazně lepší než izolovaný content failure.

## RC-4: Runtime ignoruje `SafeToRetry` a `Idempotent`

### Projev v incidentu

Child diagnostic:

```json
"retrySafety": "SafeToRetry",
"idempotency": "Idempotent"
```

Recovery decision:

```json
"decisionKind": "ManagerRequired",
"routeKind": "ManagerAction"
```

V kódu `BuildRecoveryDecision` pro každý `Blocked` stav nastavuje `ProcessRecoveryDecisionKind.ManagerRequired`. Přitom enum už obsahuje `SafeRetry` a route enum obsahuje `CurrentStepRetry`.

### Proč je to root příčina

Runtime má datový model pro automatický retry, ale nepoužívá jej. Každý completion-gate blocker tak skončí u člověka, i když je:

- deterministicky opravitelný,
- idempotentní,
- bez bezpečnostního rizika,
- v rámci stejného kroku,
- s přesným recovery plánem.

### Oprava

Změnit recovery rozhodování:

- pokud všechny relevantní diagnostiky jsou `SafeToRetry` a `Idempotent`, použít `SafeRetry` + `CurrentStepRetry`,
- přidat failure category `ProductCompletionGate` nebo podobnou,
- nerozhodovat pouze podle substringů v diagnostic code,
- použít attempt budget a fingerprint opakování,
- manager escalation až po překročení budgetu nebo při unsafe/denied/policy/no-go případu.

### Proč to pomůže

`create-dotnet-project` by po prvním selhání nedal manager escalation. Runtime by automaticky spustil cílený rework stejného kroku s instrukcí „wire solution via helper and read back“. Člověk by zasahoval až po opakovaném stejném selhání.

## RC-5: Manual rework používá obecný prompt, ne diagnosticky řízený repair packet

### Projev v incidentu

Po ručním reworku se chyba opakovala. To odpovídá kódu:

- operator service zavolá `RequestStepReworkAsync`,
- assignment repair ověří, že current agent je stále ready,
- připojí se obecný důvod,
- nepřidá se deterministický repair plan podle `product_required_file_content_missing` / missing tool receipt.

### Proč je to root příčina

Agent, který již jednou přehlédl kritický tool plan, dostane znovu dlouhý podobný brief. Bez konkrétního repair packetu má vysokou šanci zopakovat stejný mylný pattern.

### Oprava

Přidat `IProcessStepRecoveryInstructionBuilder`, který z diagnostik + launch variables + observed receipts sestaví explicitní recovery packet.

Pro tento incident má recovery packet obsahovat:

```text
Previous attempt is rejected. Do not claim solution membership from scaffold receipts.
Observed receipts: workspace_dotnet_new(sln), workspace_dotnet_new(blazorwasm). Missing receipt: workspace_pwsh_run_script.
Current solution readback: Calculator.slnx is empty.
Write DotNetCreateProjectScript verbatim to artifacts/process-runs/ab4.../scripts/create-dotnet-project.wire-solution.ps1.
Verify the script path with workspace_stat_path or workspace_read_file.
Invoke workspace_pwsh_run_script with that path, workingDirectory external-target/C/programovani/dotnet/calculator-output, and DotNetCreateProjectSideEffectManifest.
Read back Calculator.slnx or run solution list. Complete only when it contains src/Calculator/Calculator.csproj.
Do not rerun workspace_dotnet_new with force=true.
```

### Proč to pomůže

Rework už nebude „zkus to znovu“, ale konkrétní oprava rozdílu mezi expected plan a observed receipts/product state.

## RC-6: Parent subprocess bridge ztrácí child root cause

### Projev v incidentu

Parent receipt obsahuje generic:

```text
Child process run ... is Blocked
```

Ale neobsahuje child diagnostic:

```text
process.adapter.product_required_file_content_missing
Calculator.slnx does not contain src/Calculator/Calculator.csproj
```

V `ParentSubprocessArtifactBridge.ResolveExistingAsync` se stopped child, který není `Completed`, přeskočí (`continue`). Poté parent dostane jen generic stopped-child propagation z jiné vrstvy.

### Proč je to root příčina

U runtime-owned subprocess kroků je parent step často bez vlastního MAF execution runu. Pokud parent packet nepropaguje child root cause, UI/operator vidí „No AgentFramework result summary“ a „child is blocked“, ale ne skutečný důvod. To vede k blind retry nebo ručnímu klikání na rework bez cíleného zásahu.

### Oprava

Subprocess bridge musí mít výsledek typu `ChildStoppedBlocked` / `BlockedChildOutputFound`, který nese:

- child run id,
- child step instance id,
- child step key,
- child current status,
- latest child receipt diagnostics,
- child recovery decision,
- link/ref na child managed artifact a product readback evidence.

Parent diagnostic code má být např.:

```text
process.adapter.subprocess_child_blocked
```

Safe summary má přímo obsahovat child root cause.

### Proč to pomůže

Operátor i auto-recovery uvidí actionable child step, ne generic parent symptom. To je klíčové pro všechny větší procesy se subprocesy.

## RC-7: Subprocess artifact bridge je založený na file existence, ne na ledger/slot contractu

### Projev v kódu

`TryResolveChildOutputRefs` bere hardcoded child output step keys a kontroluje existenci:

```text
artifacts/process-runs/{childRunId}/steps/{stepKey}.md
```

Neověřuje, jestli child runtime skutečně přijal artifact slot do ledgeru. V incidentu child artifact fyzicky existoval, ale `ProducedArtifactsJson` bylo prázdné, protože runtime výstup odmítl.

### Proč je to root příčina

Fyzicky existující markdown není totéž jako runtime-accepted process artifact. U neúspěšných completion gates může existovat zavádějící artifact s `Status: Completed`, který byl runtime odmítnut. File-existence bridge může v jiných scénářích omylem přijmout nevalidní child evidence.

### Oprava

Parent bridge musí preferovat artifact ledger / produced artifact slots. File fallback smí být pouze recovery režim s jasnou diagnostikou, ne normální path.

### Proč to pomůže

Parent nebude stavět na artefaktu, který agent napsal, ale runtime jej neuznal.

## RC-8: MAF finalizer validuje strukturu, ne sémantiku process step contractu

### Projev v incidentu

AgentFramework log říká:

```text
Required finalizer tool 'submit_process_step_outcome' produced a valid 'process_step_outcome_result' result.
Validated structured output contract 'process_step_outcome_result'.
```

To je pravda, ale jen na úrovni JSON schema/finalizer contractu. Výstup byl strukturálně validní, ale sémanticky falešný: tvrdil solution membership bez readbacku.

### Proč je to root příčina

MAF wrapper tím legitimizuje `Completed`, které process adapter až později odmítne. To samo o sobě není chyba, pokud downstream recovery funguje. Dnes ale downstream recovery zbytečně eskaluje. Navíc „Runtime Validated Structured Outcome“ appendix se appendne před product gate rejection, což je zavádějící.

### Oprava

- Finalizer schema validation nechat, ale zavést process-semantic finalizer guard.
- Guard musí po finalizeru a před materializací/commit acceptance vyhodnotit typed gates.
- Pokud je failure safe/idempotent, provést bounded finalizer repair turn nebo current-step auto-rework.
- Appendix text přejmenovat nebo appendovat až po všech completion gates. Jinak zapisuje „validated“ do artefaktu, který runtime později odmítne.

### Proč to pomůže

MAF nebude poslední autorita pro `Completed`. Runtime semantic gate bude blíže finalizeru a nebude produkovat matoucí evidence.

## RC-9: Tool preflight je jen name-level, ne argument-level

### Projev v kódu

`ProcessRuntimeToolPreflightService` skládá dostupné tool names a porovnává je s required tool names. Neověřuje konkrétní argumenty, path, workingDirectory, sideEffectManifest ani skutečné scope.

### Proč je to root příčina

Problémy typu „chybí tool“, „nemá přístup“, „nemá kam zapsat artifact“ často vznikají až při konkrétní invokaci. Name-level preflight může projít, i když runtime později odmítne přesnou path nebo agent dostane unresolved placeholder.

### Oprava

Přidat argument-level preflight pro typed step plans:

- tool exists,
- agent má allowed operation pro daný tool,
- path je v povoleném workspace scope,
- script ref je managed artifact path a ne `steps/*.md`,
- product target mutation je jen přes allowed alias,
- sideEffectManifest je validní a odpovídá product rootu,
- placeholdery jsou resolved.

### Proč to pomůže

Řada „missing access/tool“ blockerů se odhalí před agentem nebo se promění na template/config repair, ne na runtime human escalation uprostřed procesu.

## RC-10: Template/agent kombinace není dostatečně typed

### Projev v incidentu

`create-dotnet-project` je `Work` step s role key `software-engineer`, přiřazený na `.NET Application Developer`. Ten je dobrý obecný vývojářský agent, ale krok je ve skutečnosti deterministický scaffolding executor. Role fit je moc obecný.

### Proč je to root příčina

U složitějšího procesu se kombinuje:

- dlouhý template brief,
- obecné agent instrukce,
- project structure context,
- launch variables,
- required receipts,
- finalizer pravidla,
- subprocess pravidla.

Model pak snadno mine důležitou povinnost. Samotné přidávání textových pravidel template dále zvětšuje prompt a paradoxně zvyšuje riziko přehlédnutí.

### Oprava

- Pro deterministické kroky použít runtime-owned executor.
- Pro agent-driven kroky přidat explicitní capability contract:
  - `dotnet.scaffold.solution`,
  - `dotnet.wire.solution.membership`,
  - `workspace.script.write-and-run`,
  - `process.managed-artifact.write`.
- Readiness nesmí být jen „agent má obecně workspace tools“; musí matchovat step plan.
- Template schema má nést typed plan a expected receipts, ne jen notes/launch variables.

### Proč to pomůže

Agent bude dělat to, co je skutečně agentická práce: implementace feature, design, QA posouzení. Deterministické wiring/validation si pohlídá runtime.
