# 03 — Process Messaging Policy Canvas And Runtime Enforcement

## Status

- `Closed`

## Objective

- Zavést explicitní Messaging link na process canvasu a vynucovat ho v runtime.
- Udělat z `Processes` canonical ownera direct-communication policy mezi rolemi.
- Zabránit jakémukoli agent bypassu mimo povolené role a uložit denied attempts do audit trailu.

## Covered Inputs

- `IN-06`, `IN-07`, `IN-08`, `RQ-06`, `RQ-07`, `RQ-08`, `RQ-28`, `US-06`, `US-13`

## Prerequisites

- `02-collaboration-domain-notification-and-conversation-foundation` closed.
- Process canvas foundation and Collaboration canonical store are available.

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessDefinitionEntities.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessCanvasCatalog.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.Links.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Links.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs

## Deliverables

- New process definition concept for role-to-role messaging policy.
- Canvas visual/link handling for Messaging connections.
- Runtime authorizer that computes effective permissions from process policy + agent permissions + governance state.
- Denied-path logging and allowed-path transcript persistence.

## Dependency Impact

- Launch planning, manager approval, execution orchestration and scenario validation all depend on this being correct. Pokud direct messaging není vynucené správně, celé business rule jádro je neplatné.
- UI recomposition a scenario harness budou muset dokazovat právě tuto policy.

## Validation Depth

- `Critical foundation`
- Vyžaduje component canvas proof, integration tests pro allowed/denied paths a browser proof.

## Implementation Steps

1. Přidat nový persistent model/link pro process messaging rules mezi rolemi.
2. Rozšířit canvas catalog a surface factory o Messaging connection category a vizuální pravidla.
3. Rozšířit editor actions o create/delete messaging links a jejich persistence.
4. Implementovat runtime authorization service, která se při každém message requestu dívá na process policy snapshot a agent permissions.
5. Napojit allowed messages do Collaboration canonical store a denied attempts do audit/decision evidence.

## Scope Exceptions

- Human escalation paths, které nejsou direct role-to-role, mohou vést přes Collaboration escalation route; detail manager approval integrace se řeší později.

## Do Not Do

- Nedovolit fallback „send anyway and just log it“.
- Nedělat messaging rule jako jen čistě UI dekoraci bez runtime enforcement.
- Nedublovat policy v Collaboration modulu.

## Acceptance Checklist

- Process designer umí vytvořit Messaging link mezi rolemi.
- Allowed direct message projde a uloží se do run transcriptu.
- Denied direct message bez linku failne deterministicky a je auditovatelný.
- Efektivní permission je průnik process policy, agent permission a governance state.

## Proof Required

- Component tests pro canvas link add/remove a vizuální classification.
- Integration tests pro allowed vs denied message path.
- Browser proof na `/processes` editoru s Messaging link screenshotem.
- Run detail/browser proof s uloženým transcriptem a denied-path evidence, pokud UI již existuje.

## Browser Validation Logging

- Route: `/processes` definition editor a případně run detail route.
- Viewport: `1600x900`.
- Actions: otevřít procesní canvas, přidat Messaging link, uložit, znovu načíst a ověřit vizuální přítomnost.
- Screenshot review: Messaging link je rozlišitelný od responsibility/decision/artifact lines.

## Progression Gate

- Žádná další subbundle nesmí posílat direct agent messages, dokud není policy enforcement dokazatelně funkční.
- Pokud se ukáže, že message authorization obchází process-owned service, práce se musí vrátit sem.

## Suggested Agent Prompt

```text
Implement only subbundle 03.

Add process-owned Messaging links to the canvas and a runtime authorizer that blocks any direct role-to-role communication without explicit policy. Persist allowed messages into Collaboration and audit denied attempts. Prove both allowed and denied paths.
```

## Execution Result

- Persistent role-to-role Messaging policies were added to process definition storage plus SQLite and PostgreSQL migrations.
- Definition-canvas link creation and deletion now persist Messaging links and classify them with the dedicated Messaging category and ports.
- Runtime direct messaging now enforces the effective permission intersection of process policy, run-assignment direct-messaging permission, and governance state.
- Allowed direct messages project into Collaboration and reappear as run-scoped transcript evidence.
- Denied direct messages create durable rejected decision evidence plus conformance observations instead of silently falling back.

## Validation Result

- `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\CanDoItAll.Modules.Processes.csproj`
- `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~Steps_canvas_connection_actions_create_and_delete_messaging_links_and_classify_them_visually"`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~SendDirectMessageAsync"`
- Live SQLite runtime verification against `C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\529c12060808489fad29feb5bc60dda1\db\candoitall.db` confirmed the direct-messaging migration and policy tables before browser proof.
- Playwright MCP on `http://127.0.0.1:5502/processes` created and published a real Messaging link on `Customer onboarding orchestration` v4, started the run `Messaging policy proof 2026-04-14`, recorded an allowed direct message, and verified the denied reverse-direction evidence.

## Browser Proof

- Canvas proof: `C:\repositories\CanDoItAll\agentframework-full-integration\reviews\artifacts\sb03-processes-messaging-canvas.png`
- Transcript proof: `C:\repositories\CanDoItAll\agentframework-full-integration\reviews\artifacts\sb03-processes-messaging-runtime.png`
- Denied-path proof: `C:\repositories\CanDoItAll\agentframework-full-integration\reviews\artifacts\sb03-processes-messaging-runtime-conformance.png`

## Closure Decision

- Entry gate: `Prepared`
- Closure gate: `Closed on 2026-04-14`
- Progression result: `Passed`
- Downstream dependency note: `Launch-planning and orchestration phases may now consume process-owned direct-messaging policy, transcript projection, and denied-path audit evidence without reopening subbundle 03.`

## Residual Risk

- The live runtime assignment editor and direct-message selectors still render `Unknown role` for run-scoped assignments created from the v4 published definition even though transcript and conformance evidence resolve the role names correctly. Policy enforcement is correct, but the role-label projection should be cleaned up in a downstream UI-focused phase instead of being silently ignored.

