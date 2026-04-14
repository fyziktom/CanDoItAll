# 02 — Collaboration Domain Notification And Conversation Foundation

## Status

- `Closed`

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

- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Automation/AutomationMessagingServices.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.SharedKernel/ActivityStream.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Activity/ActivityModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Web/Components/Layout/MainLayout.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Web/Program.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Composition/ModuleAssemblies.cs

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

## Closure Evidence

- Build gate passed on `2026-04-14`:
  - `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Collaboration\CanDoItAll.Modules.Collaboration.csproj`
  - `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- Migration gate passed on `2026-04-14`:
  - `dotnet ef migrations add AddCollaborationFoundation --project C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite\CanDoItAll.Migrations.Sqlite.csproj --context AppDbContext --output-dir Migrations`
  - `dotnet ef migrations add AddCollaborationFoundation --project C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --context AppDbContext --output-dir Migrations`
- Test gate passed on `2026-04-14`:
  - `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~CollaborationIntegrationTests"`
  - `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build --filter "FullyQualifiedName~MainLayoutCollaborationTests"`
- Real validation found and fixed a provider bug before closure:
  - initial integration run failed because SQLite in this stack did not support `DateTimeOffset` ordering inside `GetWorkspaceAsync`.
  - service query ordering was moved after materialization so SQLite and PostgreSQL both stay valid.
- Browser proof passed on `2026-04-14`:
  - created an escalation through the real `/collaboration` UI,
  - verified shell unread badge `1`, unread/escalation/thread counts, selected thread detail, transcript, and route-bound `threadId`,
  - verified `Unread only` filter kept the selected unread thread visible,
  - marked the selected thread as read and verified the shell badge disappeared and the unread-filtered inbox became empty,
  - retained screenshot artifacts:
    - `C:\repositories\CanDoItAll\agentframework-full-integration\reviews\artifacts\sb02-collaboration-desktop.png`
    - `C:\repositories\CanDoItAll\agentframework-full-integration\reviews\artifacts\sb02-collaboration-mobile.png`
- Visual review conclusion:
  - desktop hierarchy is readable and the two-pane layout keeps list/detail responsibilities clear,
  - unread badge state is obvious in shell and workspace tiles,
  - the `390px` mobile pass keeps reply actions visible without clipped controls.

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

