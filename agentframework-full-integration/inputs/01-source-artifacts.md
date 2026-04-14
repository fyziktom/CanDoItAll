# 01 — Source Artifacts

## User-Supplied Artifacts

- Original CanDoItAll archive: `/mnt/data/CanDoItAll-development (1).zip`
- Original AgentFramework archive: `/mnt/data/CanDoItAll.AgentFramework-main (1).zip`

## Extracted Working Copies Used For Analysis

- CanDoItAll extracted root: `/mnt/data/work/cando/CanDoItAll-development`
- AgentFramework extracted root: `/mnt/data/work/agentfw/CanDoItAll.AgentFramework-main`

## User-Expected Execution Paths

- CanDoItAll target repo on user machine: `C:\repositories\CanDoItAll`
- AgentFramework source repo on user machine: `C:\repositories\CanDoItAll.AgentFramework`

## Key Verified Solution Entrypoints

### CanDoItAll

- `/mnt/data/work/cando/CanDoItAll-development/CanDoItAll.slnx`
- `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Web/Program.cs`
- `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Composition/ModuleAssemblies.cs`
- `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Web/Composition/ShellNavigation.cs`

### AgentFramework

- `/mnt/data/work/agentfw/CanDoItAll.AgentFramework-main/CanDoItAll.AgentFramework.sln`
- `/mnt/data/work/agentfw/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `/mnt/data/work/agentfw/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Sandbox/Hosting/ScenarioHarnessSupport.cs`

## Path Translation Rule For Codex

- Všechny absolute source references v bundle ukazují na analyzované kopie pod `/mnt/data/work/...`, aby prošla lokální validace bundle.
- Při skutečné implementaci na uživatelčině stroji musí Codex přeložit:
  - `/mnt/data/work/cando/CanDoItAll-development` -> `C:\repositories\CanDoItAll`
  - `/mnt/data/work/agentfw/CanDoItAll.AgentFramework-main` -> `C:\repositories\CanDoItAll.AgentFramework`
- Bundle nepředpokládá, že externí repo zůstane po integraci připojené jako live dependency. Slouží jen jako source material pro fyzické převzetí kódu.

## Artifact Integrity Notes

- Bundle vychází z reálného obsahu obou archivů, ne z hypotetického stavu.
- Byla ověřená existence relevantních modulů CanDoItAll (`Workspace`, `Processes`, `CRM-HR`, `Automation`, `Activity`, `Security`, `Web`) i AgentFramework projektů (`Models`, `Core`, `Persistence`, `Hosting`, `Maf`, `Components`, `Sandbox`).
- Bylo ověřeno, že AgentFramework scenario harness aktuálně obsahuje scénáře `SC01–SC08`, což je důležitá odchylka proti zadání, které mluví o pěti scénářích.
