# 01 – Rekonstrukce incidentu `prepare-solution-skeleton`

## Identifikace běhu

Parent process run:

- Run id: `e5f874f1-02b9-43c8-9c2d-ee932972e992`
- Step: `prepare-solution-skeleton`
- Step instance id: `db3e7295-b523-4343-8be6-85598427385b`
- Výsledek: `NeedsManager` / `Blocked`

Child process run:

- Run id: `ab4a1ed8-8b1b-4974-973d-93983bf41f09`
- Step: `create-dotnet-project`
- Step instance id: `53d370f4-04c6-4f9c-8ce0-9cd89efda764`
- Agent execution run id: `48c3753c-d0bb-4679-9eae-2f295d2b8181`
- Výsledek: `NeedsManager` / `Blocked`

## Co se opravdu stalo

Parent step neselhal kvůli vlastnímu AgentFramework výstupu. Parent step je runtime/subprocess-driven. Proto soubor `api/agents/parent-prepare-step-execution-runs.json` vrací `[]`. UI hláška „No AgentFramework result summary was found“ je tedy diagnostická/projection mezera, ne důkaz, že k běhu neexistuje runtime receipt.

Parent step jen propagoval zastavený child proces:

```text
Child process run ab4a1ed8-8b1b-4974-973d-93983bf41f09 is Blocked
```

Skutečná technická chyba vznikla v child kroku `create-dotnet-project`.

## Co agent udělal

Child AgentFramework run použil tyto relevantní nástroje:

- `workspace_stat_path` na product root,
- `workspace_create_directory` na `external-target/C/programovani/dotnet/calculator-output/src`,
- `workspace_dotnet_new` pro `new sln -n Calculator`,
- `workspace_dotnet_new` pro `new blazorwasm -n Calculator`,
- `workspace_write_file` pro managed artifact `steps/create-dotnet-project.md`,
- `workspace_read_file`/`workspace_stat_path` pro upstream `scaffold-contract.md`.

Nepoužil:

- `workspace_pwsh_run_script`.

To je zásadní, protože právě `workspace_pwsh_run_script` měl spustit helper, který přidá app projekt do solution a ověří `dotnet sln list`.

## Co agent tvrdil

Agent v managed artifactu tvrdil:

```text
Solution membership readback
- Solution file selected for this run: external-target/C/programovani/dotnet/calculator-output/Calculator.slnx
- App project referenced by the solution: external-target/C/programovani/dotnet/calculator-output/src/Calculator/Calculator.csproj
- Readback evidence: the solution scaffold receipt and app scaffold receipt both targeted the contract paths, and the app scaffold output confirmed restore completed for the app project.
```

Toto je logická chyba agenta. `dotnet new sln` nevkládá projekt do solution. `dotnet new blazorwasm` vytvoří projekt, ale také jej automaticky nepřidá do samostatně vytvořené solution. Scaffold receipts tedy nejsou důkazem solution membership.

## Co ukázal product readback

Přímý readback `product-target/Calculator.slnx.txt`:

```xml
<Solution>
</Solution>
```

`product-target/dotnet-slnx-list.txt`:

```text
V řešení se nenašly žádné projekty.
```

Runtime tedy správně odmítl `Completed`.

## Runtime diagnostika child kroku

Child receipt:

```json
{
  "code": "process.adapter.product_required_file_content_missing",
  "safeSummary": "Step 'create-dotnet-project' claimed completion but required product file content/readback check(s) failed: C:\\programovani\\dotnet\\calculator-output\\Calculator.slnx does not contain any expected text from [src\\Calculator\\Calculator.csproj | src/Calculator/Calculator.csproj].",
  "retrySafety": "SafeToRetry",
  "idempotency": "Idempotent"
}
```

To je správná validace produktu, ale špatná recovery reakce. Recovery decision bylo:

```json
{
  "failureCategory": "Unknown",
  "decisionKind": "ManagerRequired",
  "policy": "process.manager-review-required",
  "routeKind": "ManagerAction"
}
```

Tedy: diagnostika říká „safe/idempotent“, ale runtime ji přesto eskaluje na managera.

## Proč ruční rework opakuje stejnou chybu

Ruční rework přidá obecnou instrukci. Nepřidá deterministickou recovery instrukci typu:

1. už znovu nespouštěj `dotnet new`,
2. zapiš helper script do konkrétní resolved path,
3. ověř helper přes `workspace_stat_path`,
4. spusť `workspace_pwsh_run_script`,
5. přečti solution a ověř, že obsahuje app csproj,
6. teprve potom přepiš managed artifact a finalizer.

Navíc assignment repair pouze ověřuje, zda agent stále vypadá připravený. Protože `.NET Application Developer` má obecně dostatečná práva a tooling, repair jej nepřepne ani nezmění plán. Výsledek: stejný agent dostane víceméně stejný prompt a zopakuje stejný mylný vzorec.

## Důkaz, že problém není „chybí tool“

Tool `workspace_pwsh_run_script` byl v promptu i v launch variables označen jako povinný receipt. Problém nebyl v tom, že by runtime nutně nevěděl o povinném toolu. Problém byl ve třech věcech:

1. agent jej nevolal,
2. completion gate validace nejdřív narazila na file content failure a tím skryla chybějící tool receipt,
3. safe/idempotent failure šel do manager eskalace místo auto-reworku.

## Incident timeline

1. Parent `prepare-solution-skeleton` spustil runtime-owned child `dotnet-solution-setup`.
2. Child `scaffold-contract` proběhl a vytvořil scaffold contract.
3. Child `create-dotnet-project` vytvořil solution a app project.
4. Agent přeskočil helper script a `workspace_pwsh_run_script`.
5. Agent zapsal managed artifact a vrátil `Completed`.
6. MAF finalizer validoval pouze strukturovaný `process_step_outcome_result`.
7. Process adapter zmaterializoval/appendnul managed artifact appendix „Runtime Validated Structured Outcome“.
8. Product completion gate zjistila, že solution neobsahuje projekt.
9. Adapter vrátil `NeedsManager` s `SafeToRetry/Idempotent` diagnostikou.
10. Runtime recovery klasifikace nastavila `Unknown` + `ManagerRequired`.
11. Parent step propagoval generic child block bez child root-cause detailu.
12. UI ukázalo parent hlášku bez přesného AgentFramework result summary.
