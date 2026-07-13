# 05 – Cílová separace kódu pro testovatelnost

Aktuální problém není jen v jedné podmínce. Důležité rozhodování je rozprostřené v partial třídách adapteru, runtime result helpers, dispatch service, operator service, launch variable contributorech a subprocess bridge. To ztěžuje izolované testování. Níže je návrh separace, kterou má Codex provést postupně a bezpečně.

## Nové služby a odpovědnosti

### 1. `ILaunchVariableTemplateResolver`

Odpovědnost:

- resolve placeholderů v launch variables,
- validace unresolved placeholderů v tool-critical hodnotách,
- detekce cyklů,
- audit, které hodnoty byly změněny.

Navržené API:

```csharp
public interface ILaunchVariableTemplateResolver
{
    LaunchVariableResolutionResult Resolve(
        IReadOnlyDictionary<string, string> variables,
        LaunchVariableResolutionOptions options);
}

public sealed record LaunchVariableResolutionResult(
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<LaunchVariableResolutionIssue> Issues);
```

Testy:

- `{CurrentProcessRunId}` v `DotNetCreateProjectScriptRef` se nahradí GUIDem,
- nested placeholders se nahradí v bounded počtu průchodů,
- unresolved placeholder v `*ScriptRef` failne validation,
- běžná prose poznámka může unresolved placeholder povolit pouze explicitní allowlistem.

### 2. `IProcessCompletionGateEvaluator`

Odpovědnost:

- agregovaně vyhodnotit všechny completion gates,
- vrátit primary + secondary issues,
- neprovádět recovery rozhodnutí.

Navržené API:

```csharp
public interface IProcessCompletionGateEvaluator
{
    ProcessCompletionGateEvaluation EvaluateCompleted(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts,
        Guid? executionRunId);
}
```

Rozdělit interně na:

- `IGroundedEvidenceGate`,
- `IManagedArtifactGate`,
- `IProductMutationReceiptGate`,
- `IRequiredToolReceiptGate`,
- `IProductPathGate`,
- `IProductReadbackGate`,
- `IRequiredProductStateGate`,
- `ICompletedWithoutDeclaredBlockerGate`.

Testy:

- empty `.slnx` + missing `workspace_pwsh_run_script` vrátí obě issues,
- primary issue je stabilně prioritizovaný,
- evidence hash je deterministický,
- safe/idempotent metadata se zachová.

### 3. `IRequiredToolReceiptMatcher`

Odpovědnost:

- porovnávat expected receipts proti observed tool receipts,
- rozlišit tool name, template marker, command args, workingDirectory, scriptRef, product alias.

Navržené API:

```csharp
public interface IRequiredToolReceiptMatcher
{
    RequiredToolReceiptMatchResult Match(
        IReadOnlyList<RequiredToolReceiptExpectation> expectations,
        IReadOnlyList<ToolExecutionReceiptRecord> observedReceipts);
}
```

Pro `workspace_pwsh_run_script` má očekávat:

- tool name,
- script path z resolved `DotNetCreateProjectScriptRef`,
- workingDirectory z `WorkspaceAlias`,
- sideEffectManifest ref/value.

### 4. `IProcessRecoveryClassifier`

Odpovědnost:

- rozhodnout `FailureCategory`, `DecisionKind`, `RouteKind`, policy a reason,
- používat diagnostic metadata, ne jen substringy,
- respektovat retry budget a repeated fingerprint.

Navržené API:

```csharp
public interface IProcessRecoveryClassifier
{
    ProcessRecoveryDecisionReceipt? Classify(
        StrategyResultEnvelope result,
        ProcessRuntimeStepStatus appliedStepStatus,
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState step);
}
```

Pravidla:

- all safe/idempotent completion gates → `SafeRetry/CurrentStepRetry`,
- missing upstream artifact → `UpstreamStepRework`, pokud lze najít responsible step,
- denied tool/capability → assignment repair nebo manager podle policy,
- policy/template mismatch → `TemplateRepair`,
- stopped child blocked → `ChildRunPropagation` se child diagnostic,
- repeated same safe retry fingerprint po budgetu → `ManagerRequired` s konkrétním důvodem.

### 5. `IProcessStepRecoveryInstructionBuilder`

Odpovědnost:

- generovat stručný, cílený rework packet z diagnostic issues, observed receipts a launch variables.

Navržené API:

```csharp
public interface IProcessStepRecoveryInstructionBuilder
{
    ProcessStepRecoveryInstruction Build(
        ProcessRuntimeStepAssignment assignment,
        ProcessRecoveryDecisionReceipt decision,
        IReadOnlyList<StrategyDiagnosticRef> diagnostics,
        IReadOnlyList<ToolExecutionReceiptRecord> observedReceipts);
}
```

Pro `.NET create project` musí umět:

- nerozbíjet již existující scaffold,
- doplnit helper script,
- spustit helper,
- readback solution membership,
- přepsat managed artifact až po úspěchu.

### 6. `ISubprocessRunStateResolver`

Odpovědnost:

- najít active/stopped child runs,
- načíst child runtime state,
- načíst latest child receipt/diagnostics,
- vrátit typed child state pro parent bridge.

Výsledky:

- `ChildActive`,
- `ChildCompletedAccepted`,
- `ChildCompletedNoGo`,
- `ChildCompletedWithoutAcceptedOutput`,
- `ChildStoppedBlocked`,
- `ChildStoppedFailed`,
- `NoMatchingChildRun`.

### 7. `ISubprocessArtifactBridge`

Odpovědnost:

- převádět accepted/no-go child outputs na parent outcome,
- používat artifact ledger/slot contract jako primární zdroj pravdy,
- file existence fallback jen jako explicitní recovery mode.

### 8. `IProcessStepToolPlanExecutor` / `IProcessStepToolPlanGuard`

Odpovědnost:

- pro deterministic steps provést tool plan,
- pro agent-driven guarded steps aspoň ověřit expected plan proti observed receipts a product state,
- generovat repair packet při missing plan item.

Navržené API:

```csharp
public interface IProcessStepToolPlanExecutor
{
    ValueTask<ProcessStepToolPlanExecutionResult> ExecuteAsync(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepToolPlan plan,
        CancellationToken cancellationToken);
}
```

Fáze zavedení:

1. guard only,
2. runtime-owned executor pro `.NET solution setup`,
3. generalizace pro další process templates.

## Změny ve flow

### Současný flow

```text
Agent finalizer Completed
  -> materialize/append managed artifact
  -> ToAdapterResult validations, first issue wins
  -> NeedsManager
  -> runtime BuildRecoveryDecision -> ManagerRequired
  -> dispatch applies generic manager recovery instruction
```

### Cílový flow

```text
Agent finalizer Completed
  -> resolve observed receipts
  -> evaluate completion gates aggregate
  -> if gates fail and safe/idempotent:
       create SafeRetry/CurrentStepRetry decision
       build diagnostic repair packet
       bounded automatic rework or repair turn
  -> if gates pass:
       materialize/promote managed artifact
       produce artifact slots
  -> if unsafe/policy/no-go/retry-budget exceeded:
       manager escalation with concrete root cause
```

## Kde dělat malé bezpečné kroky

### Fáze A – bezpečné bez změny template schema

- Přidat launch variable resolver a změnit testy, které dnes očekávají unresolved placeholder.
- Přidat gate aggregator, ale ponechat původní metody jako interní gates.
- Změnit recovery classifier pro safe/idempotent diagnostics.
- Přidat diagnostic-specific recovery instruction builder.
- Přidat parent child diagnostic propagation.

### Fáze B – typed plan guard

- Vygenerovat `ProcessStepToolPlan` z existujících launch variables.
- Nechat agenta provádět tool calls, ale guard vyhodnotí missing plan items.
- Při missing item auto-rework.

### Fáze C – runtime-owned executor

- Pro `.NET solution setup` kroky spustit helper přímo přes runtime tool service.
- Agent pak jen shrne a zapíše artifact, případně ho runtime vygeneruje.

### Fáze D – template schema migrace

- Přesunout subprocess contracty a tool plans do JSON schema.
- Přidat load-time validation a migration.

## Anti-regression zásady

- Neodstraňovat product completion gates. Ty správně chytily prázdnou solution.
- Neoslabovat required receipts, aby proces „prošel“.
- Nezvyšovat pouze počet retries bez cílené instrukce.
- Nepřidávat další dlouhé prompt instrukce jako hlavní řešení.
- Nepřijímat fyzický artifact jako validní evidence, pokud runtime gate selhala.
