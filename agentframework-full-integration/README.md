# CanDoItAll AgentFramework Full Integration

Tento bundle je execution-grade koordinační balíček pro plnou integraci `CanDoItAll.AgentFramework` do `CanDoItAll` jako nativního modulu, včetně nového Collaboration modulu, procesně řízeného messagingu, CRM-HR resource bindingů, staged process launch flow a recomponovaného UI.

## Profile

- `initiative`

## Mission

- Převést AgentFramework z odděleného sandboxového solution do CanDoItAll tak, aby vznikl jeden integrovaný systém se třemi jasnými canonical boundaries: `Collaboration` pro lidskou komunikaci a notifikace, `CRM-HR` pro resource pool a `AgentFramework` pro technical AI runtime. Současně odstranit duplicity provider execution logiky, zavést procesně řízenou messaging policy, změnit start procesu na staffing/approval pipeline a dodat tak přesný bundle, podle kterého Codex zvládne implementaci bez improvizace, fake testů a bez rozštěpení source-of-truth.

## Bundle Layout

- `inputs/` původní zadání, zip artefakty, strukturovaný vstup a repozitářové mapování
- `analysis/` zjištěný current state, assumptions, risks a duplication hotspots
- `requirements/` normalizované požadavky, actors, user stories a XLSX workbook
- `architecture/` cílová architektura, data/migration strategie a UI composition
- `plan/` dependency-aware phase plan, gates a validační program
- `traceability/` mapování raw notes -> requirements -> subbundles -> proof
- `shared-prompts/` reusable Codex prompts pro implementaci, QA, browser proof a refactor gates
- `subbundles/` atomické workstreamy se striktními acceptance a proof pravidly
- `inventories/` současný ownership, duplication map a scenario/test inventory
- `templates/` reusable execution log, browser log, scenario report a refactor reopen šablony
- `reviews/` self-review bundle a připravený execution report template

## Repository Mapping


| Logical repo | User machine path | Bundle analysis path | Notes |
| --- | --- | --- | --- |
| CanDoItAll | `C:\repositories\CanDoItAll` | `C:\repositories\CanDoItAll` | Cílové repo pro integraci a místo, kam se budou kopírovat zdroje AgentFrameworku. |
| CanDoItAll.AgentFramework | `C:\repositories\CanDoItAll.AgentFramework` | `C:\repositories\CanDoItAll.AgentFramework` | Zdrojová codebase, ze které se musí převzít neutral runtime, models, components a scenario harness logika. |


## Recommended Execution Order

1. `subbundles/01-foundation-import-map-and-module-skeleton`
2. `subbundles/02-collaboration-domain-notification-and-conversation-foundation`
3. `subbundles/03-process-messaging-policy-canvas-and-runtime-enforcement`
4. `subbundles/04-provider-ownership-bridge-and-legacy-runtime-retirement`
5. `subbundles/05-agent-catalog-persistence-workspace-scoping-and-governance-bridges`
6. `subbundles/06-crmhr-resource-binding-and-agent-management-surface`
7. `subbundles/07-process-launch-planning-hr-recommendation-and-default-strategies`
8. `subbundles/08-manager-approval-human-substitution-and-resource-provisioning`
9. `subbundles/09-agent-execution-orchestration-artifact-bridge-and-run-observability`
10. `subbundles/10-agent-ui-recomposition-shell-tabs-and-cross-module-experience`
11. `subbundles/11-scenario-migration-real-e2e-validation-and-playwright-proof`
12. `subbundles/12-data-backfill-cleanup-refactor-gates-and-final-closure`

## Non-Negotiable Execution Rules

- Žádná subbundle nesmí pokračovat dál, pokud její progression gate neprošla.
- Když implementace začne vytvářet dlouhé soubory, duplicity, druhý editable source of truth nebo obcházení shared helpers, musí executor nejprve vytvořit refactor subbundle a teprve potom pokračovat.
- Nesmí vzniknout paralelní provider execution path, paralelní agent registry ani paralelní process messaging path.
- Scénáře a Playwright validation se musí spouštět přes skutečné UI a skutečný runtime; ruční seednutí výsledků bez procesu je zakázané.
- Bundle používá current-state zjištěný v repozitářích, ne předpoklady z paměti. Zvlášť důležité je, že AgentFramework repo dnes obsahuje `SC01–SC08`, ne pět scénářů.

## Dependency And Validation Map

- Kritické dependency chainy, mermaid diagram a reopen triggers jsou v `plan/01-phase-plan.md`.
- User-story coverage, raw note coverage a proof mapping jsou v `traceability/`.
- Reusable validační otázky a strict refactor rule jsou v `shared-prompts/`.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed on 2026-04-15 after the 2026-04-14 audit reopen was repaired with real code, real browser proof, and full bundle closure validation`
- Subbundle gate review: `01 through 12 passed with closed execution-report gates and browser-proof coverage`
- Audit follow-up gates: `00 passed on 2026-04-14; 01 passed on 2026-04-15 after the oversized collaboration/processes/layout surfaces were split and the launch workflow service was decomposed into maintainable partials`
- Final closure gate: `Passed on 2026-04-15 through prepared/completed bundle validation, audit closure validation, targeted component and integration tests, and Playwright end-to-end proof`
- Browser validation analytics: `Recorded for subbundles 01 through 12 under reviews/artifacts/ with audit logs under reviews/browser-logs/`

## Audit Reopen

- `2026-04-14`: `agentframework-integration-audit-followup` reopened this initiative.
- The earlier completion claim was premature and the reopen was valid.
- `2026-04-15`: the reopen was resolved only after later-wave subbundles, proof logs, validators, and refactor gates were completed for real.
- `/agents` is now the integrated AgentFramework shell with working Overview, Providers, Agents, Governance, Scenarios, and diagnostics-aware tabs rather than a placeholder route.

## Execution Notes

- `2026-04-14`: subbundle `01-foundation-import-map-and-module-skeleton` closed after module skeleton wiring, `CanDoItAll.Web` build, external-reference architecture guard, and shell proof on `/agents`.
- `2026-04-14`: subbundle `02-collaboration-domain-notification-and-conversation-foundation` closed after collaboration persistence/service wiring, SQLite/PostgreSQL migrations, targeted integration/component tests, and desktop/mobile browser proof on `/collaboration`.
- `2026-04-14`: subbundle `03-process-messaging-policy-canvas-and-runtime-enforcement` closed after process-owned Messaging policy persistence, canvas link authoring, runtime transcript/audit enforcement, targeted component/integration tests, and live `/processes` browser proof on a published v4 definition plus a fresh runtime run.
- `2026-04-14`: audit follow-up reopened the initiative, required browser-proof markdown logs, required reproducible Playwright proof surfaces, and blocked any claim that the full integration is complete.
- `2026-04-14`: audit follow-up subbundle `00-mandatory-reopen-and-proof-discipline` passed after the docs were reopened truthfully, the browser-proof markdown logs were confirmed, the new `AgentFrameworkAuditProofTests` Playwright suite passed, and the hard-fail closure script passed.
- `2026-04-15`: subbundle `04-provider-ownership-bridge-and-legacy-runtime-retirement` closed after the integrated provider bridge was proven on `/agents?tab=Providers`, the duplicated settings redirect recursion bug was fixed in `SettingsPage.razor.cs`, and provider-related component tests passed.
- `2026-04-15`: subbundle `05-agent-catalog-persistence-workspace-scoping-and-governance-bridges` closed after agent profile persistence, project assignment support, governance surfacing, and the integrated `/agents` workspace proof all passed through integration and Playwright coverage.
- `2026-04-15`: subbundle `06-crmhr-resource-binding-and-agent-management-surface` closed after CRM-HR agent binding proof on `/crm-hr/agents` showed the business/technical split with provider and owner data sourced from the integrated stack.
- `2026-04-15`: subbundle `07-process-launch-planning-hr-recommendation-and-default-strategies` closed after real launch-plan creation, candidate resolution, recommendation/fallback metadata, and approval submission were validated in both integration tests and live browser proof.
- `2026-04-15`: subbundle `08-manager-approval-human-substitution-and-resource-provisioning` closed after the launch approval thread, human manager authority path, and approval-to-ready transition were validated in the calculator delivery process flow.
- `2026-04-15`: subbundle `09-agent-execution-orchestration-artifact-bridge-and-run-observability` closed after outbox retry integration tests passed and the live run detail showed managed artifacts, direct-message evidence, and execution observability on `/processes`.
- `2026-04-15`: subbundle `10-agent-ui-recomposition-shell-tabs-and-cross-module-experience` closed after `/agents` proved the integrated shell tabs on desktop and narrow layouts without a duplicate sandbox shell.
- `2026-04-15`: subbundle `11-scenario-migration-real-e2e-validation-and-playwright-proof` closed after `SC04` ran through the integrated scenario harness and the calculator process generated a simple Blazor calculator workflow with launch planning, approval, direct messaging, artifact production, and completed run metadata.
- `2026-04-15`: subbundle `12-data-backfill-cleanup-refactor-gates-and-final-closure` closed after the refactor gate was repaired for the audited collaboration/processes/layout files, `ProcessesService.Launch.cs` was split from `1668` lines into focused partials, real build/component/integration/Playwright validations passed again, and both bundle validators plus the audit closure validator passed.

