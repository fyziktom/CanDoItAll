# Subbundle 04 — Execute Remaining Wave C: Approval, Agent Execution, UI Recomposition, Scenarios

## Covers

Přísnější override pro původní subbundles:

- `08-manager-approval-human-substitution-and-resource-provisioning`
- `09-agent-execution-orchestration-artifact-bridge-and-run-observability`
- `10-agent-ui-recomposition-shell-tabs-and-cross-module-experience`
- `11-scenario-migration-real-e2e-validation-and-playwright-proof`

## Objective

Uzavřít skutečný end-to-end flow od staffing proposal až po běžící process run a reálné scenarios.

## Tasks

1. **Manager approval + human substitution**
   - project-specific Main Manager resolver
   - možnost AI agent nebo human approver
   - approval tasks musí jít přes Collaboration / notifications, ne přes hidden side channel

2. **Resource provisioning**
   - po schválení vzniknou run assignments a binding na selected resources
   - provisioning musí být auditovatelné

3. **Agent execution orchestration**
   - selected AI resources skutečně vykonávají role v runu
   - artifacts a run evidence se ukládají na process/run úroveň
   - direct messaging mezi roles respektuje Messaging policy z dřívější wave

4. **Real `/agents` UI recomposition**
   - zachovat hlavní CanDoItAll shell
   - uvnitř `/agents` dodat interní tabs / sections reprezentující původní AgentFramework Sandbox surfaces
   - minimálně: agents, providers, chat, governance, scenarios

5. **Scenario migration**
   - přenést skutečné scenarios z původního AgentFramework repo
   - minimálně respektovat existující SC01–SC08
   - přidat nové process-centric scenarios, např. collaborative app-writing flow
   - scenarios nesmí běžet bokem mimo process launch / approval / run

6. **Real E2E validation**
   - Playwright + integration tests + screenshot review
   - žádné fake seednutí finálního stavu bez process flow
   - test musí projít přes:
     role definition -> selection -> approval -> run -> messaging -> artifacts -> inspection

## Acceptance

- `/agents` je skutečný pracovní modul, ne rozcestník
- Main Manager může být agent nebo člověk podle projektu
- selected resources skutečně běží v process runu
- scenarios jsou přenesené a spustitelné přes integrovaný runtime
- evidence je uložená v repu a auditovatelná

## Fail conditions

- scenario harness bude jen zkopírovaná stránka bez vazby na processes,
- browser proof nebude commitnutý,
- approval nebo execution se bude dít mimo Collaboration / Processes canonical records.
