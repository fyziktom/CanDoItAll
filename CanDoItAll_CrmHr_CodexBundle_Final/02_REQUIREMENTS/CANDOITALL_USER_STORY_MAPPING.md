# CanDoItAll user-story mapping

| CanDoItAll area | Primary files | Mapped stories | Main gap to close |
| --- | --- | --- | --- |
| Shell navigation and workspace | src/CanDoItAll.Web/Composition/ShellNavigation.cs<br>src/CanDoItAll.Web/Components/Layout/MainLayout.razor | X-01, DIR-15, CRM-18, AI-08 | Add CRM / HR navigation entry, nested route support, tab labels, and route-aware context chips. |
| Module composition and startup | src/CanDoItAll.Web/Program.cs<br>src/CanDoItAll.Web/Composition/ModuleAssemblies.cs<br>src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs | X-05, X-08, DIR-01 | Register the new module assembly, startup seeding, and services without breaking local auto-create behavior. |
| Projects module | src/CanDoItAll.Modules.Projects/ProjectModels.cs<br>src/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor | PRJ-01, PRJ-02, PRJ-09, PRJ-10, PRJ-11, CRM-22, HR-35 | Show primary customer, delivery unit, account manager, linked opportunity, and party-based filters on project surfaces. |
| Workbench structure and calendar | src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs<br>src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs<br>src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.RichDefinitions.cs<br>src/CanDoItAll.Modules.Workbench/ProjectStructureCreateRequestComposer.cs<br>src/CanDoItAll.Modules.Workbench/ProjectStructureNodeEditor.cs | PRJ-03, PRJ-04, PRJ-05, PRJ-06, PRJ-07, PRJ-08, PRJ-15, PRJ-16, AI-05 | Replace CRM-lite participant isolation with central party references, party pickers, assignment metadata, and optional local-only projections. |
| Resources module | src/CanDoItAll.Modules.Resources/ResourceModels.cs<br>src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor | PRJ-13, CRM-21 | Add owner and maintainer party linkage so operational responsibility is visible across projects and shared resources. |
| Workspace providers and AI execution | src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs<br>src/CanDoItAll.Modules.Workspace/ProviderExecution.cs | AI-01, AI-02, AI-03, AI-04, AI-06, AI-07, AI-08 | Bind AI agent party records to provider profiles, default model strategy, ownership, and review status. |
| Activity and global search | src/CanDoItAll.Modules.Activity/ActivityModels.cs<br>src/CanDoItAll.Infrastructure/Search/SearchIndexing.cs | DIR-14, DIR-15, CRM-19, X-02, X-03, X-11 | Index parties, interactions, opportunities, workforce profiles, candidate flows, and AI agents; write meaningful CRM/HR activity entries. |
| Validation center | src/CanDoItAll.Modules.Validation/ValidationModels.cs | PRJ-12, X-03 | Link responsible owner, reviewer, or approver parties to validation runs so accountability flows through the new module. |
| Test Lab | src/CanDoItAll.Modules.TestLab/TestLabModels.cs | PRJ-12, X-03 | Link owners and reviewers to test plans and evidence where accountability matters. |
| Automation workspace | src/CanDoItAll.Modules.Automation/AutomationModels.cs | CRM-20, HR-24, HR-25, HR-33, X-15 | Add reminder jobs and job visibility for overdue next actions, onboarding tasks, and expiring contracts or allocations. |
| BaseLib UI components | docs/ui-shared-components/README.md<br>src/CanDoItAll.Components.BaseLib/Components | X-04, DIR-03, CRM-18, HR-18 | Use only BaseLib and normal HTML/Tailwind on all CRM/HR pages; do not import canvas libs. |
| Playwright and automated validation | tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs<br>tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs | X-06, X-07, X-16 | Add full CRM/HR browser flows, screenshot evidence, and semantic analysis for visual validation and regression confidence. |


## Interpretation rules used in this mapping

- If the repository already has a concept that is close to a CRM/HR need, the plan reuses and extends it instead of replacing it blindly.
- If the repository already has a project-local concept but the user asked for shared CRM/HR behavior, the plan upgrades the concept into a **shared identity + project projection** model.
- If an enterprise need exists but the current repository has no foothold for it at all, the plan introduces a new CRM/HR model and page surface rather than hiding it in an unrelated module.

## Most important mapping decisions

1. **Project participant nodes** become project projections of central parties.  
   This closes the gap between current Workbench behavior and the requested shared CRM/HR registry.

2. **AI agents** use the same shared identity model as people and organizations.  
   Provider runtime remains in `Workspace`; business identity and assignment live in CRM/HR.

3. **Project ownership and assignment** are modeled centrally.  
   Customer, partner, delivery unit, assignee, and reviewer roles become reusable across modules.

4. **Recruiting and staffing** are not side notes.  
   They are required because the user explicitly wants HR to handle company-based delivery units, person-based staffing, and project assignment.

5. **Validation, Test Lab, and Resources** gain explicit party ownership.  
   This is necessary to make the wider CanDoItAll platform relationship-aware rather than keeping CRM/HR isolated.
