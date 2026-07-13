# Executive root cause analysis

## Co pravděpodobně způsobilo konkrétní blocker

Krok `prepare-solution-skeleton` v `dotnet-development-slice` je parent subprocess step. Jeho povinností není jen vytvořit `.slnx` a projekty. Musí dodat parent artefakt `solution-skeleton-evidence`, který odkazuje na přijatý child run a jeho handoff packet.

Přiložený `calculator-output.zip` obsahuje produktovou kostru:

- `Calculator.slnx`
- `src/Calculator/Calculator.csproj`
- Blazor WebAssembly soubory
- `tests/Calculator.Tests/Calculator.Tests.csproj`
- prázdný `UnitTest1.cs`

Neobsahuje ale procesní managed artefakty typu:

- `artifacts/process-runs/<run-id>/steps/prepare-solution-skeleton.md`
- `artifacts/process-runs/<child-run-id>/steps/setup-handoff.md`
- `artifacts/process-runs/<child-run-id>/steps/setup-handoff-after-repair.md`
- runtime handoff/evidence packet pro parent slot `solution-skeleton-evidence`

To znamená: produktový vedlejší efekt pravděpodobně proběhl, ale procesní kontrakt nebyl uzavřen. Rework pak opakuje stejný krok, protože runtime neví, jestli existuje validní child handoff nebo jen náhodně vzniklé soubory.

## Proč hláška ukazuje na špatné místo

User-facing hláška:

> No AgentFramework result summary was found for this blocker; inspect execution runs by process run and step id before approving a blind retry.

nevzniká jako primární chybová zpráva agenta. Vzniká v operator projection vrstvě, když runtime má `StrategyResultReceipt`, ale `ProcessRuntimeOperatorActionDiagnostics.Create(...)` nedostane žádnou použitelnou AgentFramework observation.

To může nastat i tehdy, když skutečný důvod byl zcela konkrétní:

- missing expected output artifact,
- child subprocess completed without accepted handoff,
- child run is still active/stopped,
- `project_structure_process_subprocess_launch` nebyl dostupný/autorizovaný,
- agent vrátil `Completed`, ale finalization contract ho změnil na `NeedsManager`,
- AgentFramework `ResultSummary` zůstal prázdný nebo se ztratil kvůli špatné korelaci execution runů.

## Hlavní slabina architektury

Současný design nechává příliš mnoho odpovědnosti na prompt a příliš málo na typed runtime kontraktech.

U velkých procesů to vede ke kombinaci:

1. Agent dostane dlouhou textovou instrukci a obecné allowed operations.
2. Runtime pak validuje tvrdé kontrakty přes sloty a receipts.
3. Když agent mine jeden detail, runtime krok zablokuje.
4. Operator projection nedokáže najít přesnou AgentFramework observation.
5. Rework doplní obecnou instrukci, která neopraví skutečný tool/artifact/subprocess mismatch.
6. Krok spadne stejně.

## Prioritní architektonická oprava

`StepKind=Subprocess` by se měl chovat jako OS-level orchestration primitive:

- runtime vybere/launchne child process,
- runtime parent krok deferne, dokud child běží,
- runtime po child terminálu validuje accepted/no-go child výstupy,
- runtime vytvoří parent handoff artefakt do parent produced slotu,
- agent se k subprocess launchi nepoužívá jako hlavní řídicí mechanismus.

Agent může zůstat pro běžné work/review/validation kroky. Subprocess orchestration ale musí být deterministická runtime schopnost.
