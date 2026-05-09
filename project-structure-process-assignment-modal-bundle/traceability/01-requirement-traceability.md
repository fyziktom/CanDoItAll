# Requirement Traceability

| Raw note | Exact wording | Requirement IDs | Impacted surface | Planned proof | Owning subbundle | Status |
|---|---|---|---|---|---|---|
| IN-001 | Start action via project structure process node opens assignment process | R-001, R-007 | `ProjectStructurePage.Processes.cs`, `ProjectStructureCanvasDialogs.razor` | Component test plus browser route proof | 01, 03 | Solved |
| IN-002 | Modal must be fullscreen | R-001, R-002, R-008 | `ProjectStructureOverlayDialog.razor`, staffing dialog CSS | Browser screenshot and computed style check | 01, 03 | Solved |
| IN-003 | Follow attached design | R-002, R-003, R-004, R-005, R-008 | Staffing modal markup/CSS | Large-screen and narrow screenshots reviewed against design questions | 01, 03 | Solved |
| IN-004 | Reuse chat agent switching component with filtering and favourite tags | R-006, R-007 | `AgentSwitchDialog.razor`, `ProjectStructurePage.Processes.cs`, process launch selection service if needed | Component/service tests and open picker browser proof | 02, 03 | Solved |
| IN-005 | Validate with screenshots corresponding to attached design | R-009 | Browser proof artifacts and execution report | Screenshot paths and visual review rows | 03 | Solved |

## Requirement To Files

| Requirement | Bundle files | Primary implementation files |
|---|---|---|
| R-001 | `requirements/01-normalized-requirements.md`, subbundle 01 | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureCanvasDialogs.razor` |
| R-002 | subbundle 01 | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureOverlayDialog.razor` |
| R-003 | subbundle 01 | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureCanvasDialogs.razor.css` |
| R-004 | subbundle 01 | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureCanvasDialogs.razor` |
| R-005 | subbundle 01 | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureCanvasDialogs.razor.css` |
| R-006 | subbundle 02 | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\AgentSwitchDialog.razor`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Processes.cs` |
| R-007 | subbundle 02 | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Launch\ProcessesService.Launch.Planning.cs` if backend support is needed |
| R-008 | subbundle 03 | Browser screenshots and CSS |
| R-009 | subbundle 03 | `reviews/01-execution-report.md` |
