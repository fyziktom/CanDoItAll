# 02 — Collaboration Domain Notification And Conversation Foundation

## Status

- `Ready`

## Objective

- Založit canonical Collaboration modul pro notifikace, threads, escalation items a unread state.
- Oddělit user-facing conversation store od stávající Automation transport vrstvy.
- Vytvořit základ pro lidské schvalování a agent eskalace ještě před plnou integrací agent runtime.

## Covered Inputs

- `IN-04`, `IN-06`, `RQ-04`, `RQ-05`, část `RQ-19`, `US-04`, `US-05`, `US-07`, `US-27`

## Prerequisites

- `01-foundation-import-map-and-module-skeleton` closed with gate passed.
- Shell skeleton routes exist.

## Exact Source References

- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Automation/AutomationMessagingServices.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.SharedKernel/ActivityStream.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Activity/ActivityModels.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Web/Components/Layout/MainLayout.razor
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Web/Program.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Composition/ModuleAssemblies.cs

## Deliverables

- Collaboration entities, services, queries a DB konfigurace pro inbox, threads, participants, messages a escalations.
- MainLayout integration pro unread indicator / badge.
- Projection bridge z automation/process/agent signals do Collaboration canonical store.
- Activity projection hooks pro audit trail.

## Dependency Impact

- Subbundles 03, 08, 09, 10 a 11 budou ukládat message, approval a escalation proof do Collaboration. Bez správného modelu hrozí ztráta auditovatelných dat.
- UI recomposition potřebuje Collaboration route a badge dřív, než se připojí agent nebo process flows.

## Validation Depth

- `Critical UI foundation`
- Vyžaduje persistence, integration tests a browser proof na inbox/thread detail.

## Implementation Steps

1. Navrhnout Collaboration data model a context linking fields pro process/run/launch references.
2. Implementovat queries a write services s jasným rozlišením notification item vs conversation thread vs escalation.
3. Napojit Collaboration na existing automation transport pouze jako signal ingress, ne jako read model.
4. Promítnout audit zapsáním do `IActivityStream` a připravit search projections, pokud to dává smysl.
5. Přidat shell entry, unread badge a základní route `/collaboration` s tabs `Inbox`, `Threads`, `Escalations`.

## Scope Exceptions

- Detailní process messaging authorization se uzavírá až v subbundle 03; tady vzniká canonical store a UI foundation.

## Do Not Do

- Nepoužívat Automation tabulky jako canonical user-facing inbox.
- Nevyměňovat Activity stream za Collaboration store ani obráceně.
- Nereprezentovat notifications jen transientními toasty bez persistence.

## Acceptance Checklist

- Collaboration modul má perzistentní inbox a thread model.
- Unread state a escalation item jsou součástí canonical modelu.
- MainLayout nebo shell umí zobrazit Collaboration entry s badge/indicator.
- Automation signál lze promítnout do Collaboration store bez přímého čtení z automation tabulek.

## Proof Required

- Integration test pro založení notification itemu, threadu a message včetně context linku.
- Build affected projects.
- Playwright proof na `/collaboration` s inbox listingem a otevřením thread detailu.
- Screenshot a vizuální review unread badge / hierarchy / readability.

## Browser Validation Logging

- Route: `/collaboration`.
- Viewport: `1600x900`, plus užší pass pokud layout používá multi-pane design.
- Actions: otevřít Inbox, filtrovat unread, otevřít thread, zkontrolovat context metadata.
- Screenshot review: čitelný timeline, badge state, jasné call-to-action pro response/escalation.

## Progression Gate

- Další subbundles smějí Collaboration používat až když canonical store a UI route existují a badge/thread proof prošel.
- Pokud se ukáže, že data model neumí nést process/run context, subbundle se musí reopen-nout.

## Suggested Agent Prompt

```text
Implement only subbundle 02.

Build the Collaboration module as the canonical inbox/thread/escalation store. Reuse Automation only as transport. Add unread badge support in the shell and provide a browser-visible `/collaboration` route. Do not yet implement process messaging policy beyond the storage and context foundations.
```
