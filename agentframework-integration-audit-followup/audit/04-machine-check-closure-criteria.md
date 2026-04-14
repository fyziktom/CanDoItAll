# 04 — Machine-Check Closure Criteria

Tento checklist je tvrdý. Kdo chce tvrdit, že integrace je hotová, musí dodat průchod všemi body.

## Truthfulness checks

Před final claim musí vrátit **0 matches**:

```bash
rg -n "Execution state: `In progress`|not honestly closable yet|Pending implementation|To be filled" agentframework-full-integration
rg -n "Integrated agent module foundation|Planned imports|Later subbundles|future integrated surfaces|deferred" src/CanDoItAll.Modules.AgentFramework
```

## Structure checks

Musí existovat reálná lokální agent doména v CanDoItAll, ne jen placeholder route. Minimální očekávání:

```bash
rg -n "AgentDefinition|AgentTemplate|AgentCapability|AgentExecution|ScenarioHarness" src
rg -n "LaunchPlan|ApprovalResolver|Provisioning|ResourceRecommendation" src/CanDoItAll.Modules.Processes src/CanDoItAll.Modules.CrmHr src/CanDoItAll.Modules.AgentFramework
```

## Proof checks

Musí existovat commitnuté artifacts a review notes:

```bash
find agentframework-full-integration/reviews -type f | sort
find agentframework-full-integration/reviews/artifacts -type f | sort
```

## Automation checks

Musí existovat reprodukovatelné testy pro kritické flows:

- `/agents`
- `/crm-hr/agents`
- `/collaboration`
- process launch planning + approval
- process run with selected resources
- scenario harness end to end

Neuznávej pouze volný text v execution reportu. Musí být přiložené buď:
- automatizované testy v repu,
- nebo uložené MCP/browser proof logs + screenshots + review notes.

## Quality gates

Před final closure:

- žádný nový non-generated soubor nesmí bez výjimky růst přes 400 řádků, pokud neexistuje schválená refactor note,
- nesmí vzniknout druhý editable source of truth pro providers ani agents,
- `StartRunAsync` nesmí z UI obcházet launch-planning approval flow.
