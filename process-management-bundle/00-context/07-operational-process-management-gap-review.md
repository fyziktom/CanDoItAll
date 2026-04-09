# Operational process-management gap review

This document maps the large-enterprise process-management blind spots raised in the review passes to explicit bundle responses.

## OP-01 — Jasné vlastnictví procesu

**Why it matters**: Bez skutečného end-to-end ownera se problémy přehazují mezi útvary.

**Bundle response**: Publish guardrails pro process owner/customer/value statement + governance profile.

**Mapped features**: PRM-F17, PRM-F20

**Guardrail / note**: Owner je povinný pro governed publish; stewardship může být víceúrovňový.

## OP-02 — End-to-end pohled místo pohledu oddělení

**Why it matters**: Největší ztráty vznikají na předávkách mezi týmy.

**Bundle response**: Interface contracts, upstream/downstream boundaries, done definitions, interface telemetry.

**Mapped features**: PRM-F17, PRM-F19

**Guardrail / note**: Rozhraní jsou samostatné artefakty, ne jen poznámka v diagramu.

## OP-03 — Rozdíl mezi procesem a organizační strukturou

**Why it matters**: Org chart není totéž co tok hodnoty.

**Bundle response**: ADR odděluje value flow od reporting lines; role binding jde přes CRM-HR bez vnucení hierarchie.

**Mapped features**: PRM-F03, PRM-F17

**Guardrail / note**: Graf procesu nesmí kopírovat org chart pouze proto, že existuje.

## OP-04 — Měření správných metrik

**Why it matters**: Aktivita není výsledek.

**Bundle response**: Outcome telemetry: lead/touch/queue time, first-time-right, rework, customer signal.

**Mapped features**: PRM-F19, PRM-F14

**Guardrail / note**: Dashboard nesmí vydávat activity counts za hlavní KPI.

## OP-05 — Variabilita a výjimky

**Why it matters**: Happy path nestačí pro reálný provoz.

**Bundle response**: Approved variants, exception playbooks, override reasons, escalation handling.

**Mapped features**: PRM-F18, PRM-F21

**Guardrail / note**: Výjimky jsou modelované, journalované a reviewovatelné.

## OP-06 — Kvalita vstupů do procesu

**Why it matters**: Garbage in, garbage out.

**Bundle response**: Input-quality rules, completeness checks, triage classification, rework reasons.

**Mapped features**: PRM-F04, PRM-F18

**Guardrail / note**: Vstupní kvalita je kontrolovatelná před pokračováním procesu.

## OP-07 — Procesní disciplína vs. procesní byrokracie

**Why it matters**: Příliš mnoho kontrol vede k obcházení procesu.

**Bundle response**: Risk-tiered controls and rationalization review.

**Mapped features**: PRM-F18, PRM-F20

**Guardrail / note**: Nízkoriziková práce nemá být utopená v approvals.

## OP-08 — Role středního managementu

**Why it matters**: Bez aktivní podpory středního managementu proces formálně žije, ale reálně ne.

**Bundle response**: Stewardship roles, change acknowledgements, management adoption flows.

**Mapped features**: PRM-F17, PRM-F20

**Guardrail / note**: Manager není jen approver, ale steward/adoption owner.

## OP-09 — Reálná práce vs. proces na papíře

**Why it matters**: Oficiální diagram může být fikce.

**Bundle response**: Conformance observations, deviation clustering, paper-vs-reality reviews.

**Mapped features**: PRM-F21

**Guardrail / note**: Základem je evidence z run journalu a řízené terénní pozorování.

## OP-10 — Rozhraní mezi procesy

**Why it matters**: Nejasný konec a začátek procesů vytváří chaos.

**Bundle response**: Explicit process interface contracts and done definitions.

**Mapped features**: PRM-F17

**Guardrail / note**: Styčné plochy mají vstupy, výstupy a převzetí odpovědnosti.

## OP-11 — Prioritizace procesů

**Why it matters**: Ne vše musí být modelováno stejně hluboko.

**Bundle response**: Criticality tiers and portfolio prioritization.

**Mapped features**: PRM-F17, PRM-F20

**Guardrail / note**: Governance hloubka se odvíjí od criticality a business impact.

## OP-12 — Proces bez vazby na strategii

**Why it matters**: Optimalizace bez byznys vazby může být zbytečná.

**Bundle response**: Strategic objective link on governed processes and change requests.

**Mapped features**: PRM-F17, PRM-F20

**Guardrail / note**: Proces má business intent, ne jen existenci.

## OP-13 — Záměna standardizace za rigiditu

**Why it matters**: Standard musí nechat prostor pro řízené varianty.

**Bundle response**: Mandatory vs conditional controls and approved variants.

**Mapped features**: PRM-F18

**Guardrail / note**: Co je pevné a co flexibilní se modeluje explicitně.

## OP-14 — Nedostatečná procesní gramotnost lidí

**Why it matters**: Bez porozumění vzniká jen administrativa navíc.

**Bundle response**: Role-based guidance, glossary/help, communication/acknowledgement.

**Mapped features**: PRM-F20

**Guardrail / note**: Literacy je součást provozu, ne jen rollout deck.

## OP-15 — Chybějící governance změn procesu

**Why it matters**: Bez řízení změn vznikají neoficiální varianty.

**Bundle response**: Governed change requests, impact analysis, approvals, rollout communication.

**Mapped features**: PRM-F20, PRM-F02

**Guardrail / note**: Immutable versioning samo o sobě nestačí.

## OP-16 — Nepřiznané neformální mocenské struktury

**Why it matters**: Ignorování neformálních vlivů zkresluje pochopení reality.

**Bundle response**: Restricted operational observations and deviation patterns instead of rumor registry.

**Mapped features**: PRM-F21

**Guardrail / note**: Pouze privacy-safe evidence-oriented notes, ne „kdo koho zná“ databáze.

## OP-17 — Absence decision rights

**Why it matters**: Není jasné, kdo může co rozhodnout a v jakém limitu.

**Bundle response**: Canonical decision-right rules with thresholds and override routes.

**Mapped features**: PRM-F18, PRM-F06

**Guardrail / note**: Rozhodovací práva nejsou schovaná v textu či kultuře.

## OP-18 — Neřešení kapacit a úzkých míst

**Why it matters**: Mapa procesu sama neřeší bottlenecky.

**Bundle response**: Capacity signals and bottleneck analysis tied to telemetry and staffing.

**Mapped features**: PRM-F19, PRM-F07

**Guardrail / note**: Kapacita a bottlenecky jsou součást metrics vrstvy.

## OP-19 — Podcenění času předávek a čekání

**Why it matters**: Často se optimalizuje práce, ale ne čekání.

**Bundle response**: Queue/wait-state telemetry and wait-reason reporting.

**Mapped features**: PRM-F19

**Guardrail / note**: Wait time je první-class metrika.

## OP-20 — Nepochopení, pro koho je proces zákazník

**Why it matters**: Bez zákazníka se proces řídí podle interní pohodlnosti.

**Bundle response**: Primary customer field + customer-value metrics.

**Mapped features**: PRM-F17, PRM-F19

**Guardrail / note**: Zákazník může být interní nebo externí, ale musí být explicitní.

## OP-21 — Skryté zavazbení agentů mimo proces

**Why it matters**: Když se agenti propojí přímo mimo procesní model, ztratí se odpovědnost, audit i řízení handoffů.

**Bundle response**: Process-native work briefs, baton artifacts, governed triage records, a break-glass override path, and ADR that keeps collaboration topology in the process model.

**Mapped features**: PRM-F22, PRM-F24

**Guardrail / note**: Produkční agent collaboration nemá obcházet proces bez výslovného override a journal evidence.

## OP-22 — Dvojí registry a osiřelý runtime kontext

**Why it matters**: Oddělené registry template/provider/capability a nesvázané runtime sessions vedou k chaosu, neauditovatelnosti a driftu mezi business a runtime vrstvou.

**Bundle response**: CRM-HR/Workspace canonical ownership, external execution correlations, and explicit AgentFramework convergence rules.

**Mapped features**: PRM-F23, PRM-F16, PRM-F13

**Guardrail / note**: Runtime může být externí, ale business truth a correlation zůstávají v CanDoItAll.
