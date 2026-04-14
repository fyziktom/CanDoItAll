# 01 — Foundation Import Map And Module Skeleton

## Status

- `Ready`

## Objective

- Založit fyzickou integrační kostru v cílovém repo, aby další práce běžela už jen uvnitř CanDoItAll.
- Vynutit source-of-truth boundaries, feature gates a zákaz externích project references na původní AgentFramework solution.
- Rozdělit cílový import na `Collaboration` a `AgentFramework` moduly místo jednoho sandboxového monolitu.

## Covered Inputs

- `IN-01`, `IN-02`, `IN-15`, `IN-16`, `RQ-01`, `RQ-03`, `RQ-04`, `RQ-23`
- Zakládá fyzické předpoklady pro všechny další subbundles.

## Prerequisites

- Validated bundle ve stavu `prepared`.
- Read and accept `analysis/03-duplication-and-shared-asset-map.md` a `architecture/01-target-solution.md`.

## Exact Source References

- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Web/Program.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Composition/ModuleAssemblies.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Web/Composition/ShellNavigation.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Workspace/ProviderExecution.cs
- /mnt/data/work/agentfw/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs
- /mnt/data/work/agentfw/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Sandbox/Components/Pages/Home.razor
- /mnt/data/work/agentfw/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Sandbox/Components/Pages/Agents.razor

## Deliverables

- Nové project skeletony `CanDoItAll.Modules.Collaboration` a `CanDoItAll.Modules.AgentFramework` přidané do solution a composition rootu.
- Import map z AgentFramework repo do cílové folder struktury uvnitř CanDoItAll.
- Guardrails proti externím references a proti paralelnímu provider runtime pathu.
- Základní shell entries / placeholder routes pro nové moduly, aby následné browser proofy měly stabilní kotevní body.

## Dependency Impact

- Downstream subbundles závisejí na tom, že moduly a namespaces mají finální směr. Když se to udělá špatně, všechen následující proof bude navázaný na špatnou strukturu.
- Slabý výsledek tady přímo znehodnotí migrace, UI recomposition i test asset wiring.

## Validation Depth

- `Critical foundation`
- Vyžaduje build + architecture guard + minimální shell/browser proof.

## Implementation Steps

1. Vytvořit nové moduly pod `src/` a připojit je do `CanDoItAll.slnx`, `Program.cs`, `ModuleAssemblies.cs` a `ShellNavigation.cs`.
2. Navrhnout a zapsat fyzický copy map z AgentFramework repo do cílových folderů `Domain`, `Runtime`, `Persistence`, `Components`, `Pages`, `Composition`.
3. Zkopírovat jen neutrální / adaptačně vhodné části AgentFrameworku; sandbox host bootstrap nevnášet jako druhou app.
4. Založit feature gates nebo explicitní no-op placeholders pro legacy provider execution retirement a CRM-HR technical binding delegation.
5. Průběžně hlídat, aby nevznikly externí project references ani dočasné „shim classes“ bez ownershipu.

## Scope Exceptions

- V této fázi se ještě neuzavírá žádný business flow; cílem je architektonická kostra, ne finální funkcionalita.

## Do Not Do

- Nepřipojovat projekt z `C:\repositories\CanDoItAll.AgentFramework` jako reference.
- Nekopírovat sandbox shell/program/bootstrap tak, aby vznikla druhá aplikace uvnitř CanDoItAll.Web.
- Nevytvářet už teď plné runtime service implementace bez jasného ownershipu.

## Acceptance Checklist

- Nové moduly existují a buildí.
- Shell obsahuje nové module entries nebo aspoň placeholder routes.
- Neexistuje live external reference na původní AgentFramework solution.
- Executor má explicitně zapsané, které source části se budou kopírovat a které se zahodí nebo recomposují.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- Architecture guard test nebo grep/diff proof, že solution/reference neodkazují na externí AgentFramework repo.
- Playwright nebo host/browser proof, že shell ukáže nové module entry bez rozbití layoutu.

## Browser Validation Logging

- Route: `/agents` a případně `/collaboration` placeholder.
- Viewport: desktop `1600x900`.
- Actions: otevřít shell navigation, ověřit novou menu položku, zachytit screenshot shellu.
- Screenshot review: žádné duplicitní menu, žádný druhý shell, žádné layout clipping.

## Progression Gate

- Do další subbundle je dovoleno jít až ve chvíli, kdy buildí skeleton moduly a neexistuje externí reference ani duplicitní shell path.
- Pokud se v této fázi objeví nesourodá folder/namespace struktura, musí se nejdřív vyčistit.

## Suggested Agent Prompt

```text
Implement only subbundle 01.

Create the physical merge skeleton inside CanDoItAll. Add new Collaboration and AgentFramework modules, connect them to Program/Composition/Web shell, and establish copy boundaries for imported AgentFramework code. Do not implement business behavior yet beyond the minimal placeholders needed for build and shell proof. Prove that there is no external project reference back to the AgentFramework repo and no second application shell.
```
