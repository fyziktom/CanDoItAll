# Source Artifacts

## Repository Surfaces Inspected

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.RunStart.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.StepTransitions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessOutbox.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.GovernedOutcomes.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeProgressionPlanner.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplateProjectionService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplatePackLoader.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Launch.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Launch.CandidateDiscovery.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Launch.Staffing.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Launch.Provisioning.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentSupport.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentCatalogService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentRuntime.cs`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\software-delivery\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\ai-assisted-change-delivery\definition.json`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessMockAgentRuntimeIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessOutboxIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\ProcessTemplatePackLoaderTests.cs`

## Validation Commands Executed During Analysis

- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests|FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessOutboxIntegrationTests"`
- `dotnet test tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --no-restore --filter "FullyQualifiedName~ProcessTemplatePackLoaderTests|FullyQualifiedName~ProcessTemplateCatalogServiceTests|FullyQualifiedName~ProcessTemplateProjectionServiceTests|FullyQualifiedName~CurrentArchitectureTemplateParityTests"`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.StartRunAsync_prefills_project_role_binding_and_persists_runtime_signals|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_requires_recorded_required_artifacts_before_completion|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_routes_selected_branch_and_skips_the_non_selected_path|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_requires_branch_outcome_when_conditional_dependents_exist|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_waits_for_all_dependencies_before_join_step_becomes_ready|FullyQualifiedName~ProcessesServiceIntegrationTests.GetEditorAsync_and_publish_clone_preserve_artifact_input_links|FullyQualifiedName~ProcessesServiceIntegrationTests.Template_services_keep_role_and_artifact_editor_mapping_rules_aligned"`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests"`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessOutboxIntegrationTests"`

## Analysis Evidence Files

- `C:\repositories\CanDoItAll\codex\bundles\process-run-with-agents-fix\evidence\01-test-results.md`
