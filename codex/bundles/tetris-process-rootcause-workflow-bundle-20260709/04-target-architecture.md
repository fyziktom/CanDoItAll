# Target architecture

## Principy

1. Process runtime, dispatcher a branch engine musí zůstat generické.
2. .NET, Blazor, Tetris, scaffold checks a workspace dotnet tool knowledge patří do izolovaných .NET/software-delivery vrstev.
3. Completion gates nesmí být jen „vše nebo manager“. Musí umět tři výstupy:
   - current-step retry,
   - branch route,
   - manager/action escalation.
4. Prompt recovery je sekundární obrana. Primární routování musí být deterministické v runtime.

## Navržené komponenty

### `ProcessCompletionGateEvaluator`

Pure service bez znalosti MAF. Vstup:

- `ProcessRuntimeStepAssignment`,
- `ProcessStepOutcomeResult`,
- current-run receipt index,
- product/root file accessor,
- completion rule set,
- branch routing metadata.

Výstup:

- `ProcessCompletionGateEvaluation`,
- ordered issues,
- skipped/applicable rules,
- route decision.

### `ProcessCompletionReceiptRuleResolver`

Zodpovědnost:

- parsovat legacy string arrays,
- parsovat structured object rules,
- aplikovat step scope,
- aplikovat branch outcome scope,
- deduplikovat product/process receipt rules.

Legacy compatibility:

- plain `"workspace_dotnet_run"` znamená unconditional completion receipt jako dnes.
- object rule umožní branch/purpose metadata.

### `ProcessRequiredToolReceiptEvaluator`

Zodpovědnost:

- počítat matching receipts,
- ověřit `RequireCurrentRun`,
- ověřit successful/failed policy,
- vracet missing rules grouped by purpose/source.

Nemá znát `qa-validation`, `.NET`, `Blazor`, ani `Tetris`.

### `ProcessCompletionIssueRouter`

Zodpovědnost:

- rozhodnout, zda issue patří na same-step retry, branch route nebo manager.
- použít template metadata, například `CompletionIssueRoutes`.
- při branch route vytvořit branch signal a runtime gate evidence.

Příklad generického route metadata:

```json
{
  "stepKey": "qa-validation",
  "routes": [
    {
      "issueCode": "process.adapter.product_required_file_content_missing",
      "whenBranchOutcomeKeys": ["quality-accepted"],
      "routeKind": "BranchOutcome",
      "targetBranchOutcomeKey": "repair-required",
      "summary": "Deterministic acceptance content gate failed; route to repair."
    }
  ]
}
```

Toto metadata je v template/process definition/launch variables, ne v generic core hardcode.

### `IProcessRecoveryAdviceProvider`

Generic recovery builder má jen orchestration:

- posbírá diagnostics,
- najde provider podle process key/template/domain/capabilities,
- složí generic + provider-specific advice.

Provider examples:

- `GenericProcessRecoveryAdviceProvider`,
- `DotNetSoftwareDeliveryRecoveryAdviceProvider`,
- `SubprocessRecoveryAdviceProvider`.

Tím zmizí .NET a QA step-key znalost z `CanDoItAll.Processes.Application`.

### `ProcessCompletionEvaluationTrace`

Každé vyhodnocení gates by mělo uložit trace:

- output status,
- branch outcome key,
- applicable receipt rules,
- skipped receipt rules + reason,
- content checks,
- issue routing decision,
- branch route target,
- current execution run id,
- receipt source names.

Tento trace je zásadní pro debugging bez ručního ponoření do MAF logs.

## Structured receipt rule schema

Minimální návrh:

```json
{
  "toolName": "workspace_dotnet_run",
  "purpose": "AcceptanceUiProof",
  "requireCurrentRun": true,
  "requireSuccessfulExit": true,
  "minimumCount": 1,
  "enforceBranchOutcomeKeys": ["quality-accepted"],
  "skipBranchOutcomeKeys": [],
  "reason": "Visible UI acceptance requires current-run browser proof."
}
```

Pro validation tools:

```json
{
  "toolName": "workspace_dotnet_build",
  "purpose": "ValidationProof",
  "requireCurrentRun": true,
  "requireSuccessfulExit": false,
  "enforceBranchOutcomeKeys": ["quality-accepted", "repair-required"],
  "reason": "QA must run build before accepting or routing validation failure."
}
```

`requireSuccessfulExit=false` neznamená, že failed build je acceptance. Znamená, že failed build receipt je validní důkaz pro repair branch.

## Branch-routed gate evidence

Když runtime přepíše `quality-accepted` na `repair-required` kvůli deterministic gate failure, musí downstream kroky dostat jasnou evidence. Doporučení:

- vytvořit/appendnout managed artifact section `## Runtime gate findings`, nebo
- vytvořit sibling artifact `artifacts/process-runs/<run>/steps/<step>.runtime-gate-findings.md`,
- přidat tento ref do evidenceRefs,
- branch signal summary musí obsahovat issue code a krátký safe summary.

Bez toho by downstream `quality-repair` četl QA artifact, který tvrdí acceptance, a repair důvod by byl skrytý jen v runtime diagnostics.

## DotNet/software-delivery boundaries

Allowed in generic core:

- branch outcome keys as data,
- receipt rules as data,
- issue codes,
- process state transitions,
- file content check abstraction,
- evidence refs,
- current-run receipt matching.

Not allowed in generic core:

- `qa-validation`, `quality-accepted`, `repair-required`,
- `workspace_dotnet_run`, `workspace_dotnet_build`,
- `Counter.razor`, `Weather.razor`, `Blazor`, `Tetris`,
- default scaffold forbidden text.

Allowed in .NET/workbench/templates:

- .NET tool names,
- Blazor scaffold detection,
- software-delivery branch names,
- acceptance criteria extraction for .NET apps,
- runtime browser proof requirements.
