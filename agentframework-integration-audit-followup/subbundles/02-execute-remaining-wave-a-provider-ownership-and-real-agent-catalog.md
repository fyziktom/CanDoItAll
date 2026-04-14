# Subbundle 02 — Execute Remaining Wave A: Provider Ownership And Real Agent Catalog

## Covers

Přísnější override pro původní subbundles:

- `04-provider-ownership-bridge-and-legacy-runtime-retirement`
- `05-agent-catalog-persistence-workspace-scoping-and-governance-bridges`

## Objective

Dostat do CanDoItAll **skutečný** AgentFramework, ne jen shell entry.

## Tasks

1. **Copy real AgentFramework source into CanDoItAll**
   - použít source repo `C:\repositories\CanDoItAll.AgentFramework`
   - nepřidávat externí project references
   - přenést reálné doménové modely, orchestration, persistence a UI assets
   - žádný fake import typu „route + buttons + TODO later“

2. **Create canonical technical AI domain**
   - `AgentDefinition`
   - `AgentTemplate`
   - capabilities / permissions / governance
   - provider execution contracts
   - chat/runtime orchestration
   - workspace/project scoping where appropriate

3. **Retire legacy provider ownership**
   - provider ownership nesmí zůstat roztříštěný mezi Settings/CRM-HR a nový modul
   - dovolený je pouze přechodový read-only bridge nebo redirect, ne druhý editable owner

4. **Migrations and backfill**
   - migration strategy musí být explicitní
   - backfill starých provider profiles a AI agent profiles do nové canonical struktury

5. **Proof**
   - integration tests pro provider CRUD / health / execution path
   - tests pro agent catalog CRUD / template usage / workspace scoping
   - browser proof na `/agents` se skutečnými tabs a daty

## Required negative checks

Před closure musí vrátit 0 matches:

```bash
rg -n "Integrated agent module foundation|Planned imports|Later subbundles|deferred" src/CanDoItAll.Modules.AgentFramework
```

## Acceptance

- `/agents` už není placeholder,
- `AddAgentFrameworkModule()` registruje reálné služby,
- provider execution path má jediného canonical ownera,
- imported AgentFramework capability je skutečně lokální součást CanDoItAll solution.

## Fail conditions

- zůstane placeholder page,
- zůstane druhý editable provider source of truth,
- dojde pouze k přejmenování stávajících CRM-HR profilů bez technické agent domény.
