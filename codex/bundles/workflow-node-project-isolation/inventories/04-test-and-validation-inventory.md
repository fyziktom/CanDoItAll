# Test And Validation Inventory

| Test area | Current references | Planned use |
| --- | --- | --- |
| Workflow architecture boundary | `repo://tests/CanDoItAll.Tests.Unit/WorkflowArchitectureBoundaryTests.cs` | Expand to new project dependency guardrails. |
| Workflow foundations | `repo://tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs` | Preserve runtime manager/backend behavior. |
| Workflow executors | `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorPolicyObservabilityTests.cs` | Move/expand for executor abstractions and category projects. |
| Workflow catalog | `repo://tests/CanDoItAll.Tests.Unit/WorkflowCatalogTests.cs` | Preserve catalog save/import/export and validation. |
| Template loader | `repo://tests/CanDoItAll.Tests.Unit/WorkflowTemplatePackLoaderTests.cs` | Move with `Workflows.Templates`; add template parity. |
| Preview simulation | `repo://tests/CanDoItAll.Tests.Unit/WorkflowPreviewSimulationTests.cs` | Prove deterministic no-side-effect preview across moved executors. |
| Project-structure workflow nodes | `repo://tests/CanDoItAll.Tests.Unit/ProjectStructureWorkflowNodeKeysTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProjectStructureWorkflowPreviewSimulationSupportTests.cs` | Preserve Workbench adoption behavior. |
| Workbench agent workflow tools | `repo://src/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`, integration coverage to add near project-structure agent tool tests | Prove agent-tool workflow add/create/start/status methods consume isolated runtime services and keep governed lease/access behavior. |
| Scheduler workflow input options | `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerWorkflowInputSchemaService.cs`, `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerWorkflowInputOptionService.cs` | Prove scheduler consumers keep workflow input schema/options after template/runtime extraction. |
| Cognitive Memory workflow executors | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs`, focused tests to add in unit or integration suite | Prove module-provided executor descriptor parity, settings parsing, semantic dependency handling, cancellation, and diagnostic redaction after executor abstraction migration. |
| API integration | `repo://tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs` | Regression proof after service adoption. |
| Plugin catalog | `repo://tests/CanDoItAll.Tests.Integration/PluginCatalogIntegrationTests.cs` | Descriptor/source/grant/package compatibility proof. |
| Email workflow | `repo://tests/CanDoItAll.Tests.Integration/EmailWorkflowSwitchScenarioTests.cs` | Gmail/Office365 side-effect and template regression proof. |
| Component UI | `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`, `repo://tests/CanDoItAll.Tests.Components/WorkflowExecutorDisplayAdapterTests.cs` | UI service-boundary regression proof. |
| Playwright | `repo://tests/CanDoItAll.Tests.Playwright/WorkflowShellSmokeTests.cs`, `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureWorkflows.cs` | Browser proof after UI/Workbench adoption. |

## Required Validation Lanes

- Build: focused new projects first, then relevant solution slices, then full solution when final.
- Unit: project-boundary, service behavior, descriptor parity, settings schema, side-effect policy.
- Integration: API, plugin catalog, runtime package registration, workflow run lifecycle, email workflow scenario.
- Feature-module executor integration: Cognitive Memory executor registration, descriptor parity, unavailable dependency diagnostics, and no MAF/Core executor-contract dependency after abstraction migration.
- UI/component: workflow canvas/executor catalog display, plugin executor display, Workbench workflow node panels.
- Playwright: workflow shell, project-structure workflow creation/start/status path.
- Performance: focused scans at SB05, SB09, SB13 and targeted benchmarks if scans identify hot-path risk.
- Diagnostics: typed failure envelope tests, no-generic-error assertions, redaction tests, retryability classification tests, repair-hint tests, and UI/API display tests for failed workflow nodes.
- Maintainability: file-size/responsibility scans for moved large classes and tests proving helpers are isolated by parsing, settings validation, IO/provider calls, policy, result shaping, and diagnostic mapping.
