# Requirement Traceability

| Requirement | Inputs | Files | Subbundle | Proof |
| --- | --- | --- | --- | --- |
| `REQ-001` | `N001`, `N002`, `N003` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasRecompositionService.cs` | `02-definition-recomposition-tuning` | Targeted component test plus no-overlap assertion. |
| `REQ-002` | `N004` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasRecompositionService.cs` | `02-definition-recomposition-tuning` | Test proving default route remains on the primary lane. |
| `REQ-003` | `N005` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasRecompositionService.cs` | `02-definition-recomposition-tuning` | Test proving role X/Y is near linked step anchors. |
| `REQ-004` | `N006` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasRecompositionService.cs` | `02-definition-recomposition-tuning` | Test proving column/lane spacing and no overlaps. |
| `REQ-005` | `N007` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.Persistence.cs` | `02-definition-recomposition-tuning` | No persistence or manual movement changes; existing tests remain green. |
| `REQ-006` | `N001`-`N007` | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasRecompositionServiceTests.cs` | `03-validation-and-browser-proof` | Test command output and browser analytics row. |
| `REQ-007` | `N008`, `N009` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasSurfaceFactory.cs`; `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasSurfaceFactory.Links.cs`; `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasBranching.cs` | `04-role-instance-composition-and-default-template-repair` | Surface tests prove one role contract can produce multiple visual role nodes and links use the related instance. |
| `REQ-008` | `N010` | `C:\repositories\CanDoItAll\Templates\Processes\processes\*\definition.json` | `04-role-instance-composition-and-default-template-repair` | Projection/template validation plus browser proof on a default process. |

## Raw Note Closure Matrix

| Raw note | Normalized requirement | Impacted surface | Planned proof | Owning subbundle | Exception status |
| --- | --- | --- | --- | --- | --- |
| `N001` | `REQ-001`, `REQ-006` | Process canvas authoring | Component tests and browser proof | `02`, `03` | None |
| `N002` | `REQ-001` | Process canvas authoring | Layout algorithm assertions | `02` | None |
| `N003` | `REQ-001`, `REQ-006` | Process canvas authoring | Screenshot review | `03` | None |
| `N004` | `REQ-002` | Step and branch positions | Default-route lane assertion | `02` | None |
| `N005` | `REQ-003` | Role nodes and responsibility links | Role-anchor assertion | `02` | None |
| `N006` | `REQ-004` | Step spacing and links | Geometry assertions and screenshot review | `02`, `03` | None |
| `N007` | `REQ-005` | Existing canvas behavior | Existing test coverage and diff review | `02`, `03` | None |
| `N008` | `REQ-007` | Role nodes and responsibility links | Surface factory test and browser proof | `04` | None |
| `N009` | `REQ-007` | Role identity resolution | Resolver/action tests or existing action flow validation | `04` | None |
| `N010` | `REQ-008` | Default process templates | Template coordinate regeneration and browser proof | `04` | None |
