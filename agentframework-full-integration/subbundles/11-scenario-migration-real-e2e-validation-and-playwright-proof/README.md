# 11 — Scenario Migration Real E2E Validation And Playwright Proof

## Status

- `Ready`

## Objective

- Spustit reálnou end-to-end validaci na skutečných scénářích a nových process-centric test flows.
- Respektovat, že v repo dnes existuje `SC01–SC08`, a rozšířit validaci o nové scénáře bez fake bypassu.
- Dokázat, že staffing, approvals, messaging policy a run orchestration fungují v celém systému spolu.

## Covered Inputs

- `IN-18`, `IN-19`, `IN-20`, `RQ-24`, `RQ-25`, `RQ-26`, `US-23`, `US-24`, `US-25`, `US-27`

## Prerequisites

- `10-agent-ui-recomposition-shell-tabs-and-cross-module-experience` closed.
- All runtime-critical subbundles (03, 07, 08, 09) closed.

## Exact Source References

- /mnt/data/work/agentfw/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Sandbox/Hosting/ScenarioHarnessSupport.cs
- /mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Playwright/AiAgentFlowTests.cs
- /mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Playwright/StaffingFlowTests.cs
- /mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- /mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Integration/AiAgentProfileIntegrationTests.cs
- /mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs

## Deliverables

- Integrated scenario harness under `/agents?tab=Scenarios` or equivalent.
- Validation plan and actual proof for existing scenarios `SC01–SC08`.
- New scenarios `SC09–SC11` covering process staffing, human escalation and multi-agent app-writing process.
- Execution report evidence with screenshots, artifacts, run ids and notes about manual-only cases.

## Dependency Impact

- Je to hlavní pravdomluvný closure gate. Pokud tady integrace neobstojí, implementace není hotová bez ohledu na jednotlivé green tests.
- Final cleanup a review se smějí dělat až po tomto subbundle.

## Validation Depth

- `End-to-end regression and closure`
- Vyžaduje real runs, Playwright, screenshot review, artifacts a story coverage mapping.

## Implementation Steps

1. Převést scenario harness do integrated hostu a zkontrolovat existing `SC01–SC08` inventory.
2. Automatizovat nebo explicitně naplánovat manuální proof pro `SC07` a `SC08` s důvodem, proč zůstávají manuální.
3. Přidat nové scénáře `SC09–SC11`, včetně nových template agents a process definitions, aby se ověřila spolupráce agentů přes process launch flow.
4. Spustit scénáře přes skutečný UI a runtime, uložit evidence artifacts a propojit je s process/agent runs.
5. Vyhodnotit user-story coverage a doplnit UI mezery, pokud některý flow není dokončitelný.

## Scope Exceptions

- Provider-native comparison může zůstat částečně manuální, ale musí mít explicitní důkaz, ne jen poznámku `skipped`.

## Do Not Do

- Neseedovat výsledné stavy přímo do databáze a nevydávat to za scenario proof.
- Nevynechávat skutečné staffing approval a messaging policy části v nových process-centric scénářích.
- Nezavírat final bundle bez zaznamenaných run ids / artifact paths / screenshotů.

## Acceptance Checklist

- Existing scenarios `SC01–SC08` jsou pokryté integrated host proofem nebo explicitním manuálním protokolem.
- New scenarios `SC09–SC11` běží přes skutečný process launch/runtime flow.
- Execution report obsahuje run evidence, artifacts a screenshot review findings.
- Story coverage review neobsahuje neřešené UI gaps.

## Proof Required

- Targeted Playwright runs pro agents/scenarios/process flows.
- Real scenario artifacts a run ids zaznamenané v execution reportu.
- Integration/build test suite rerun after scenario setup.
- Manual protocol for `SC07` and `SC08` if automation is intentionally not used.

## Browser Validation Logging

- Route: `/agents?tab=Scenarios`, `/processes` launch/run routes, `/collaboration` as needed.
- Viewport: `1600x900` desktop plus additional pass for scenario-specific screens if needed.
- Actions: run scenario, observe progress/result, open run details/artifacts, screenshot each critical step.
- Screenshot review: end-to-end story is understandable and visibly uses the integrated UI, not hidden shortcuts.

## Progression Gate

- Final cleanup cannot start until at least one full process-centric scenario proves resource selection, approval, messaging policy and run completion together.
- Any failed scenario is a blocker until repaired or explicitly explained with a bounded follow-up subbundle.

## Suggested Agent Prompt

```text
Implement only subbundle 11.

Run real end-to-end validation using the actual integrated scenario harness and the new process-centric scenarios. Respect the current SC01–SC08 inventory, add SC09–SC11, and capture real run/artifact/screenshot evidence. No fake shortcuts are allowed.
```
