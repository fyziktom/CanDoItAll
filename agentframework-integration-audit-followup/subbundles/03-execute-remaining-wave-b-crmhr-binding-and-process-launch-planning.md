# Subbundle 03 — Execute Remaining Wave B: CRM-HR Binding And Process Launch Planning

## Covers

Přísnější override pro původní subbundles:

- `06-crmhr-resource-binding-and-agent-management-surface`
- `07-process-launch-planning-hr-recommendation-and-default-strategies`

## Objective

Napojit technical agent domain na business resource pool a přepsat start procesu na požadovaný staffing/approval flow.

## Tasks

1. **CRM-HR binding model**
   - business AI agent resource zůstává v CRM-HR resource pool
   - technical execution definition žije v AgentFramework
   - přidat explicitní binding entity / bridge service
   - UI v CRM-HR musí umět spravovat agent resource bez duplikace technického source of truth

2. **Process launch plan**
   - zavést `LaunchPlan` / `LaunchPlanRoleSelection` / `LaunchPlanStatus` nebo ekvivalent
   - `StartRunAsync` nesmí být business-first entrypoint pro UI flow
   - nový flow:
     1. snapshot roles
     2. request candidates
     3. HR recommendation
     4. approval gate
     5. provisioning
     6. actual run start

3. **HR recommendation engine**
   - využít CRM-HR resource pool
   - navrhovat existující resources i creation proposals
   - candidate matrix musí ukazovat people + AI agents + gaps

4. **Default fallback strategies**
   - dodat rule-based HR strategy bez AI provideru
   - dodat rule-based Main Manager default strategy jako fallback dependency pro další wave

5. **Block bypass**
   - UI nesmí dovolit přeskočit launch planning
   - process runtime nesmí přijmout start bez splněných gate podmínek, pokud jde o standardní launch flow

## Proof

- integration tests pro launch plan creation, candidate recommendation a blocked early-run behavior
- browser proof pro start procesu přes candidate matrix
- explicitní test na rule-based fallback bez AI provideru

## Acceptance

- proces už se z business UI nespouští rovnou do active runu,
- CRM-HR ukazuje resources včetně AI agents přes binding na technical domain,
- HR recommendation reálně vrací candidates a creation proposals,
- fallback režim funguje i bez AI.

## Fail conditions

- launch plan bude jen view model bez persistence,
- bude možné dál spustit run přímo bez approval gates,
- vznikne druhý editable registry agentů mezi CRM-HR a AgentFramework.
