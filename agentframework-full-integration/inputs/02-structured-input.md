# 02 — Structured Input

## Primary Outcome

- Přidat AgentFramework do CanDoItAll jako nativní modul a zároveň přestavět okolní architekturu tak, aby:
  - AI runtime byl centralizovaný,
  - CRM-HR zůstal canonical resource poolem,
  - process runtime vynucoval messaging i staffing governance,
  - notifikace a lidské eskalace měly vlastní durable Collaboration vrstvu.

## Hard Constraints

- Kód AgentFrameworku se musí fyzicky přesunout / zkopírovat do CanDoItAll repo; žádné externí project references.
- Agenti nesmí komunikovat napřímo mimo procesní policy.
- CRM-HR UI musí dál fungovat jako business-facing resource management pro AI agenty.
- AgentFramework sandbox UI se nesmí jen vložit jako druhý shell; musí se recomposovat do CanDoItAll.Web.
- Validace musí být reálná: buildy, testy, Playwright, screenshots, scenario runs a story coverage.

## Architectural Tensions To Resolve

- Workspace dnes vlastní provider data a současně i provider execution vrstvu.
- CRM-HR dnes vlastní business AI agent profile, ale AgentFramework má vlastní technical `AgentDefinition`.
- Automation messaging je durable transport, ale není user-facing conversation store.
- Process runtime dnes umí jen jednoduché prefill assignmenty z project assignments; neexistuje staged staffing/approval launch flow.
- AgentFramework dnes používá globální file-backed sandbox workspace a není scoped by project/process/run.

## Verified Current-State Facts That Change The Design

- CanDoItAll už má extension seam `IProcessExecutorRegistryBridge`, ale DI do něj registruje pouze `NoopProcessExecutorRegistryBridge`.
- CRM-HR už obsahuje `AiAgentProfile`, `ProjectPartyAssignment` a `StaffingRequest`, tedy business resource foundations už existují.
- Process canvas dnes zná link categories `flow`, `decision-role`, `role-binding`, `artifact`, ale ne `messaging`.
- Workspace dnes obsahuje persistent `ProviderProfile` a runtime `ProviderRegistry` + adapters.
- AgentFramework scenario harness má v repozitáři osm scénářů (`SC01–SC08`), ne pět.

## Design Decision Summary

- Založit dva nové moduly:
  - `CanDoItAll.Modules.Collaboration`
  - `CanDoItAll.Modules.AgentFramework`
- `CRM-HR` ponechat jako canonical owner resource identities.
- `Workspace/Security` ponechat jako canonical owner provider master dat a secrets.
- `AgentFramework` udělat canonical owner AI runtime, agent definitions, technical governance a scenario harness logic.
- `Processes` nechat jako canonical owner role modelu, launch orchestrace a messaging policy.
- `Automation` použít jako transport/outbox backplane, nikoli jako user-facing conversation store.
- `Activity` použít jako audit/projection sink, nikoli jako canonical inbox.

## Expected Output Of This Bundle

- Normalizované požadavky a user stories.
- Architecture pack se source-of-truth matrix, module boundaries, migration plan a UI composition.
- Atomické subbundles s prompts, gates, acceptance a proof pravidly.
- XLSX workbook pro actors/stories/requirements/traceability.
- Self-review bundle z pohledu QA, development managerky a senior C# architektky.

## Explicit Open Questions Already Resolved In This Bundle

- **Má Collaboration být součást AgentFramework modulu?** Ne. Je to samostatný modul, protože lidská komunikace a notifikace jsou širší platform concern a musí existovat před agent runtime integrací.
- **Kdo je canonical owner providerů?** Master data a secrets zůstávají ve `Workspace/Security`; execution runtime se přesouvá do AgentFrameworku.
- **Kdo je canonical owner agentů?** Business resource identity je v CRM-HR; technical executable definition v AgentFrameworku; binding mezi nimi je explicitní.
- **Mají se použít existující staffing foundations?** Ano, ale procesní launch orchestrace je vlastní doména `Processes`; CRM-HR staffing request se používá jako supporting projection / demand artifact, ne jako owner process startu.
