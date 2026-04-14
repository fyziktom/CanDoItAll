# 03 — Process Messaging Policy Canvas And Runtime Enforcement

## Status

- `Ready`

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

- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Processes/ProcessDefinitionEntities.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Processes/ProcessCanvasCatalog.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.Links.cs
- /mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Links.cs
- /mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs
- /mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs

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
