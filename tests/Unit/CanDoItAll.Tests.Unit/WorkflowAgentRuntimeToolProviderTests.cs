using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowAgentRuntimeToolProviderTests
{
    private static readonly JsonSerializerOptions ResultJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    [Fact]
    public async Task ProviderExposesFiveGovernedToolsWithAuthoritativeMetadata()
    {
        var harness = CreateHarness();

        var tools = await harness.Provider.CreateToolsAsync(harness.Context, CancellationToken.None);
        var metadata = harness.Provider.GetToolMetadata(harness.Context);

        Assert.Equal(5, tools.Count);
        Assert.Equal(5, metadata.Count);
        Assert.Equal(940, harness.Provider.Order);
        Assert.Equal(WorkflowAgentRuntimeToolProvider.ProviderKey, harness.Provider.Descriptor.ProviderKey);
        Assert.Equal(
            [
                AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList,
                AgentToolInvocationPolicyMetadata.WorkflowsExternalResponseSubmit,
                AgentToolInvocationPolicyMetadata.WorkflowsRunCancel,
                AgentToolInvocationPolicyMetadata.WorkflowsRunStart,
                AgentToolInvocationPolicyMetadata.WorkflowsRunStatusGet
            ],
            tools.Select(tool => tool.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        AssertMetadata(
            metadata,
            AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList,
            AgentRuntimeToolOperationKind.Read,
            requiresApproval: false);
        AssertMetadata(
            metadata,
            AgentToolInvocationPolicyMetadata.WorkflowsRunStatusGet,
            AgentRuntimeToolOperationKind.Read,
            requiresApproval: false);
        AssertMetadata(
            metadata,
            AgentToolInvocationPolicyMetadata.WorkflowsRunStart,
            AgentRuntimeToolOperationKind.Mutation,
            requiresApproval: true);
        AssertMetadata(
            metadata,
            AgentToolInvocationPolicyMetadata.WorkflowsRunCancel,
            AgentRuntimeToolOperationKind.Mutation,
            requiresApproval: true);
        AssertMetadata(
            metadata,
            AgentToolInvocationPolicyMetadata.WorkflowsExternalResponseSubmit,
            AgentRuntimeToolOperationKind.Mutation,
            requiresApproval: true);
        Assert.False(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(
            AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList));
        Assert.False(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(
            AgentToolInvocationPolicyMetadata.WorkflowsRunStatusGet));
        Assert.True(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(
            AgentToolInvocationPolicyMetadata.WorkflowsRunStart));
        Assert.True(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(
            AgentToolInvocationPolicyMetadata.WorkflowsRunCancel));
        Assert.True(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(
            AgentToolInvocationPolicyMetadata.WorkflowsExternalResponseSubmit));
    }

    [Fact]
    public async Task RuntimeComposerWrapsOnlyWorkflowMutationsUnlessHostSuppressesApproval()
    {
        var harness = CreateHarness();
        var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(harness.Provider);
        using var serviceProvider = services.BuildServiceProvider();
        var composer = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), serviceProvider);

        var governed = await composer.CreateCapabilityStateAsync(
            harness.Context.Agent,
            harness.Context.Provider,
            harness.Context.Capabilities,
            [],
            WorkspaceRuntimeServicesTestFactory.Create(Path.GetTempPath()),
            static (_, _, _) => Task.CompletedTask,
            CancellationToken.None,
            suppressApprovalRequirements: false);
        var suppressed = await composer.CreateCapabilityStateAsync(
            harness.Context.Agent,
            harness.Context.Provider,
            harness.Context.Capabilities,
            [],
            WorkspaceRuntimeServicesTestFactory.Create(Path.GetTempPath()),
            static (_, _, _) => Task.CompletedTask,
            CancellationToken.None,
            suppressApprovalRequirements: true);

        Assert.IsNotType<ApprovalRequiredAIFunction>(GetTool(
            governed.Tools,
            AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList));
        Assert.IsNotType<ApprovalRequiredAIFunction>(GetTool(
            governed.Tools,
            AgentToolInvocationPolicyMetadata.WorkflowsRunStatusGet));
        Assert.IsType<ApprovalRequiredAIFunction>(GetTool(
            governed.Tools,
            AgentToolInvocationPolicyMetadata.WorkflowsRunStart));
        Assert.IsType<ApprovalRequiredAIFunction>(GetTool(
            governed.Tools,
            AgentToolInvocationPolicyMetadata.WorkflowsRunCancel));
        Assert.IsType<ApprovalRequiredAIFunction>(GetTool(
            governed.Tools,
            AgentToolInvocationPolicyMetadata.WorkflowsExternalResponseSubmit));
        Assert.All(
            suppressed.Tools.Where(tool => tool.Name.StartsWith("workflows_", StringComparison.Ordinal)),
            tool => Assert.IsNotType<ApprovalRequiredAIFunction>(tool));
    }

    [Fact]
    public async Task ListToolReturnsLatestActiveVersionEvenWhenLatestCatalogItemIsDraft()
    {
        var workflowId = WorkflowId.New();
        var active = CreateDefinition(workflowId, WorkflowVersionId.New(), WorkflowLifecycleStatus.Active, "Active version");
        var draft = CreateDefinition(workflowId, WorkflowVersionId.New(), WorkflowLifecycleStatus.Draft, "Draft version");
        var catalog = new RecordingWorkflowCatalog
        {
            Items =
            [
                new WorkflowCatalogItem(
                    draft.Id,
                    draft.VersionId,
                    draft.Name,
                    draft.Description,
                    draft.Status,
                    draft.RuntimePolicy.PreferredBackend,
                    draft.UpdatedAtUtc)
            ],
            ActiveDefinitions =
            {
                [workflowId] = new WorkflowDefinitionDetail(active, WorkflowValidationResult.Success)
            }
        };
        var harness = CreateHarness(catalog: catalog);
        var tool = await GetToolAsync(
            harness.Provider,
            harness.Context,
            AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList);

        var result = await InvokeAsync<WorkflowAgentDefinitionListResult>(tool);

        var listed = Assert.Single(result.Definitions);
        Assert.Equal(active.Id.Value, listed.WorkflowId);
        Assert.Equal(active.VersionId.Value, listed.VersionId);
        Assert.Equal(WorkflowLifecycleStatus.Active, active.Status);
    }

    [Fact]
    public async Task StartToolUsesGovernedAgentOriginAndExplicitVersionSelection()
    {
        var launchService = new RecordingWorkflowLaunchService();
        var harness = CreateHarness(
            launchService: launchService,
            purpose: AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
            runtimeSessionKey: "runtime-session-42",
            contextIntent: CreateProcessIntent("process-correlation-9"));
        var tool = await GetToolAsync(
            harness.Provider,
            harness.Context,
            AgentToolInvocationPolicyMetadata.WorkflowsRunStart);
        var workflowId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var exactRequest = new WorkflowAgentStartInput(
            workflowId,
            WorkflowAgentDefinitionSelectionMode.ExactSavedVersion,
            versionId,
            "{\"ticket\":\"CAD-42\"}",
            "agent-retry-CAD-42");

        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeAsync<WorkflowAgentStartResult>(
            tool,
            new WorkflowAgentStartInput(
                workflowId,
                WorkflowAgentDefinitionSelectionMode.LatestActive)));

        var exactResult = await InvokeAsync<WorkflowAgentStartResult>(
            tool,
            exactRequest);
        _ = await InvokeAsync<WorkflowAgentStartResult>(tool, exactRequest);
        _ = await InvokeAsync<WorkflowAgentStartResult>(
            tool,
            new WorkflowAgentStartInput(
                workflowId,
                WorkflowAgentDefinitionSelectionMode.LatestActive,
                idempotencyKey: "agent-latest-CAD-42"));

        Assert.Equal(3, launchService.Intents.Count);
        var exactIntent = launchService.Intents[0];
        var exactSelection = Assert.IsType<WorkflowDefinitionSelection.ExactSavedVersion>(exactIntent.Selection);
        Assert.Equal(workflowId, exactSelection.WorkflowId.Value);
        Assert.Equal(versionId, exactSelection.VersionId.Value);
        Assert.Equal(WorkflowLaunchMode.Production, exactIntent.Mode);
        Assert.Equal(WorkflowLaunchCompletionPolicy.WaitForStopped, exactIntent.CompletionPolicy);
        var exactIdempotency = Assert.IsType<WorkflowLaunchIdempotency.CallerSupplied>(exactIntent.Idempotency);
        var retryIdempotency = Assert.IsType<WorkflowLaunchIdempotency.CallerSupplied>(launchService.Intents[1].Idempotency);
        Assert.Equal("agent-retry-CAD-42", exactIdempotency.Key.Value);
        Assert.Equal(exactIdempotency.Key, retryIdempotency.Key);
        Assert.Null(exactIntent.RequestedBackend);
        Assert.False(exactIntent.PreviewSimulationPlan.HasSteps);
        var origin = Assert.IsType<WorkflowLaunchOrigin.AgentRuntimeInvocation>(exactIntent.Origin);
        Assert.Equal(WorkflowLaunchActorKind.Agent, origin.Agent.Kind);
        Assert.Equal(harness.Context.Agent.Id.ToString("D"), origin.Agent.SubjectId);
        Assert.Equal("runtime-session-42", origin.RuntimeSessionId.Value);
        Assert.Equal("process-correlation-9", origin.CorrelationId.Value);
        Assert.Equal(AgentRuntimeToolProviderPurpose.GovernedProcessAutomation.ToString(), origin.Purpose);
        Assert.Equal(harness.Context.Governance?.WorkspaceScope, origin.AuthorizationScope);
        Assert.Equal(
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            origin.AuthorizationPolicyFingerprint);
        Assert.IsType<WorkflowDefinitionSelection.LatestActive>(launchService.Intents[2].Selection);
        Assert.Equal(WorkflowAgentDefinitionSelectionMode.ExactSavedVersion, exactResult.SelectionMode);
        Assert.Equal(WorkflowLaunchIdempotencyDisposition.EnforcedNewRun, exactResult.IdempotencyDisposition);
    }

    [Fact]
    public async Task StatusCancellationAndResponseToolsPreserveTypedRuntimeOutcomes()
    {
        var run = CreateRun(WorkflowRunState.WaitingForInput);
        var externalRequestId = WorkflowExternalRequestId.New();
        var externalRequest = new WorkflowExternalRequestRecord(
            externalRequestId,
            run.RunId,
            WorkflowExternalRequestKind.HumanInput,
            new WorkflowNodeId("human"),
            "human-input",
            "{}",
            string.Empty,
            run.UpdatedAtUtc,
            RespondedAtUtc: null);
        var runtime = new RecordingWorkflowRuntimeManager
        {
            Run = run,
            CancellationResult = new WorkflowRunCancellationResult(
                WorkflowRunCancellationOutcome.BackendNotCancellable,
                run,
                "Backend cannot cancel.")
        };
        var externalResponses = new RecordingWorkflowExternalResponseService
        {
            Result = new WorkflowExternalResponseServiceResult(
                WorkflowExternalResponseServiceOutcome.RetryableFailure,
                Operation: null,
                run,
                externalRequest,
                NextRequest: null,
                Replayed: false,
                "Resume is temporarily unavailable.")
        };
        var harness = CreateHarness(
            runtimeManager: runtime,
            externalResponseService: externalResponses);
        var statusTool = await GetToolAsync(
            harness.Provider,
            harness.Context,
            AgentToolInvocationPolicyMetadata.WorkflowsRunStatusGet);
        var cancelTool = await GetToolAsync(
            harness.Provider,
            harness.Context,
            AgentToolInvocationPolicyMetadata.WorkflowsRunCancel);
        var responseTool = await GetToolAsync(
            harness.Provider,
            harness.Context,
            AgentToolInvocationPolicyMetadata.WorkflowsExternalResponseSubmit);

        var status = await InvokeAsync<WorkflowAgentRunStatusResult>(
            statusTool,
            new WorkflowAgentRunInput(run.RunId.Value));
        var cancellation = await InvokeAsync<WorkflowAgentCancellationResult>(
            cancelTool,
            new WorkflowAgentRunInput(run.RunId.Value));
        var response = await InvokeAsync<WorkflowAgentExternalResponseResult>(
            responseTool,
            new WorkflowAgentExternalResponseInput(
                externalRequestId.Value,
                WorkflowExternalRequestVersion.Initial.Value,
                JsonSerializer.SerializeToElement(new { answer = "yes" }),
                "agent-response-42"));

        Assert.True(status.Found);
        Assert.Equal(WorkflowRunState.WaitingForInput, status.Run?.State);
        Assert.Equal(WorkflowRunCancellationOutcome.BackendNotCancellable, cancellation.Outcome);
        Assert.False(cancellation.Succeeded);
        Assert.Equal(WorkflowExternalResponseServiceOutcome.RetryableFailure, response.Outcome);
        Assert.False(response.Succeeded);
        Assert.Equal(WorkflowRunState.WaitingForInput, response.Run?.State);
        Assert.Equal(externalRequestId, externalResponses.Command?.RequestId);
        Assert.Equal(WorkflowExternalRequestVersion.Initial, externalResponses.Command?.ExpectedRequestVersion);
        Assert.Equal("yes", externalResponses.Command?.Response.GetProperty("answer").GetString());
        Assert.Equal("agent-response-42", externalResponses.Command?.IdempotencyKey.Value);
        Assert.Equal(
            WorkflowExternalResponseTrustedChannel.AgentTool,
            Assert.IsType<WorkflowExternalResponseActorContext.Authenticated>(
                externalResponses.Command?.ActorContext).Channel);

        runtime.Run = null;
        var missing = await InvokeAsync<WorkflowAgentRunStatusResult>(
            statusTool,
            new WorkflowAgentRunInput(Guid.NewGuid()));
        Assert.Equal(WorkflowAgentRunLookupOutcome.NotFound, missing.Outcome);
    }

    [Fact]
    public async Task ProviderRequiresToolPermissionAndModuleRegistersItAsScoped()
    {
        var harness = CreateHarness();
        var deniedContext = harness.Context with
        {
            Agent = harness.Context.Agent with
            {
                Permissions = harness.Context.Agent.Permissions with { CanUseTools = false }
            }
        };

        Assert.Empty(await harness.Provider.CreateToolsAsync(deniedContext, CancellationToken.None));
        Assert.Empty(harness.Provider.GetToolMetadata(deniedContext));

        var services = new ServiceCollection();
        services.AddAgentFrameworkModule(new ConfigurationBuilder().Build());
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(WorkflowAgentRuntimeAuthorizationService) &&
                          descriptor.ImplementationType == typeof(WorkflowAgentRuntimeAuthorizationService) &&
                          descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IAgentRuntimeToolProvider) &&
                          descriptor.ImplementationType == typeof(WorkflowAgentRuntimeToolProvider) &&
                          descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IWorkflowExternalRequestAuthorizer) &&
                          descriptor.ImplementationType == typeof(WorkflowExternalRequestAuthorizer) &&
                          descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IWorkflowExternalResponseActorContextFactory) &&
                          descriptor.ImplementationType == typeof(WorkflowExternalResponseActorContextFactory) &&
                          descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IWorkflowExternalResponsePageActorContextProvider));

        services.AddAgentFrameworkUi();

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IWorkflowExternalResponsePageActorContextProvider) &&
                          descriptor.ImplementationType == typeof(WorkflowExternalResponsePageActorContextProvider) &&
                          descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void WebCompositionAddsAgentFrameworkUiAfterRuntimeModules()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "App",
            "CanDoItAll.Web",
            "Program.cs"));
        var runtimeModulesIndex = source.IndexOf(
            "builder.Services.AddCanDoItAllRuntimeModules(",
            StringComparison.Ordinal);
        var agentFrameworkUiIndex = source.IndexOf(
            "builder.Services.AddAgentFrameworkUi();",
            StringComparison.Ordinal);

        Assert.True(runtimeModulesIndex >= 0, "Web composition must register runtime modules.");
        Assert.True(
            agentFrameworkUiIndex > runtimeModulesIndex,
            "AgentFramework UI services must be registered after runtime modules.");
    }

    [Fact]
    public void CapabilityMappingCoversEveryWorkflowRuntimeToolExactlyOnce()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList] = WorkflowRuntimeCapabilityKeys.DefinitionsList,
            [AgentToolInvocationPolicyMetadata.WorkflowsRunStart] = WorkflowRuntimeCapabilityKeys.RunStart,
            [AgentToolInvocationPolicyMetadata.WorkflowsRunStatusGet] = WorkflowRuntimeCapabilityKeys.RunStatusGet,
            [AgentToolInvocationPolicyMetadata.WorkflowsRunCancel] = WorkflowRuntimeCapabilityKeys.RunCancel,
            [AgentToolInvocationPolicyMetadata.WorkflowsExternalResponseSubmit] = WorkflowRuntimeCapabilityKeys.ExternalResponseSubmit
        };

        Assert.Equal(expected.Count, WorkflowAgentCapabilityKeys.ToolNameToCapabilityKey.Count);
        Assert.All(expected, item => Assert.Equal(
            item.Value,
            WorkflowAgentCapabilityKeys.ToolNameToCapabilityKey[item.Key]));
        Assert.Equal(5, WorkflowAgentCapabilityKeys.Keys.Count);
        Assert.Equal(
            WorkflowRuntimeCapabilityKeys.Keys.OrderBy(item => item, StringComparer.Ordinal),
            WorkflowAgentCapabilityKeys.ToolNameToCapabilityKey.Values.OrderBy(item => item, StringComparer.Ordinal));
    }

    [Fact]
    public async Task ProviderFailsClosedForIneligibleActorsAndAmbiguousOrDriftedAssignments()
    {
        var harness = CreateHarness([WorkflowRuntimeCapabilityKeys.DefinitionsList]);
        var assignment = Assert.Single(harness.Context.Agent.Capabilities);
        var catalogItem = Assert.Single(harness.Context.Capabilities);
        var contexts = new[]
        {
            harness.Context with
            {
                Agent = harness.Context.Agent with { Id = Guid.Empty }
            },
            harness.Context with
            {
                Agent = harness.Context.Agent with { Status = AgentLifecycleStatus.Suspended }
            },
            harness.Context with
            {
                Agent = harness.Context.Agent with { IsTemplate = true }
            },
            harness.Context with
            {
                Purpose = (AgentRuntimeToolProviderPurpose)999
            },
            harness.Context with
            {
                Agent = harness.Context.Agent with { Capabilities = [] }
            },
            harness.Context with
            {
                Agent = harness.Context.Agent with { Capabilities = [assignment, assignment] }
            },
            harness.Context with
            {
                Agent = harness.Context.Agent with
                {
                    Capabilities = [assignment with { Kind = CapabilityKind.Skill }]
                }
            },
            harness.Context with
            {
                Capabilities = [catalogItem with { Key = $"{catalogItem.Key}-drifted" }]
            },
            harness.Context with
            {
                Capabilities = [catalogItem with { Id = Guid.NewGuid() }]
            },
            harness.Context with
            {
                Capabilities = [catalogItem with { Kind = CapabilityKind.Skill }]
            },
            harness.Context with
            {
                Capabilities = [catalogItem, catalogItem]
            }
        };

        foreach (var context in contexts)
        {
            Assert.Empty(await harness.Provider.CreateToolsAsync(context, CancellationToken.None));
            Assert.Empty(harness.Provider.GetToolMetadata(context));
        }

        var wrongCase = CreateHarness([WorkflowRuntimeCapabilityKeys.DefinitionsList.ToUpperInvariant()]);
        Assert.Empty(await wrongCase.Provider.CreateToolsAsync(wrongCase.Context, CancellationToken.None));
        Assert.Empty(wrongCase.Provider.GetToolMetadata(wrongCase.Context));
    }

    [Fact]
    public async Task OneExactAssignmentExposesOnlyItsMappedToolForEverySupportedPurpose()
    {
        foreach (var mapping in WorkflowAgentCapabilityKeys.ToolNameToCapabilityKey)
        {
            var harness = CreateHarness([mapping.Value]);
            Assert.Equal(
                Enum.GetValues<AgentRuntimeToolProviderPurpose>(),
                harness.Provider.Descriptor.SupportedPurposes.OrderBy(item => item));

            foreach (var purpose in harness.Provider.Descriptor.SupportedPurposes)
            {
                var context = harness.Context with { Purpose = purpose };
                var tool = Assert.Single(await harness.Provider.CreateToolsAsync(context, CancellationToken.None));
                var metadata = Assert.Single(harness.Provider.GetToolMetadata(context));

                Assert.Equal(mapping.Key, tool.Name);
                Assert.Equal(tool.Name, metadata.ToolName);
            }
        }
    }

    [Fact]
    public async Task AttachedToolReauthorizesActorAndCatalogAtInvocationTime()
    {
        var harness = CreateHarness([WorkflowRuntimeCapabilityKeys.DefinitionsList]);
        var tool = Assert.IsAssignableFrom<AIFunction>(Assert.Single(
            await harness.Provider.CreateToolsAsync(harness.Context, CancellationToken.None)));

        harness.Workspace.Agents =
        [
            harness.Context.Agent with { Capabilities = [] }
        ];
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tool.InvokeAsync(new AIFunctionArguments()).AsTask());

        harness.Workspace.Agents = [harness.Context.Agent];
        harness.Workspace.Capabilities = [];
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tool.InvokeAsync(new AIFunctionArguments()).AsTask());

        harness.Workspace.Capabilities = harness.Context.Capabilities;
        harness.Workspace.Agents = [harness.Context.Agent, harness.Context.Agent];
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tool.InvokeAsync(new AIFunctionArguments()).AsTask());

        harness.Workspace.Agents =
        [
            harness.Context.Agent with { Status = AgentLifecycleStatus.Suspended }
        ];
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tool.InvokeAsync(new AIFunctionArguments()).AsTask());
    }

    [Fact]
    public void ToolInputsRejectEmptyOrContradictoryIdentifiers()
    {
        var response = JsonSerializer.SerializeToElement(new { answer = "yes" });
        Assert.Throws<ArgumentException>(() => new WorkflowAgentRunInput(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new WorkflowAgentExternalResponseInput(
            Guid.Empty,
            1,
            response,
            "response-1"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowAgentExternalResponseInput(
            Guid.NewGuid(),
            0,
            response,
            "response-1"));
        Assert.Throws<ArgumentException>(() => new WorkflowAgentExternalResponseInput(
            Guid.NewGuid(),
            1,
            default,
            "response-1"));
        Assert.Throws<ArgumentException>(() => new WorkflowAgentExternalResponseInput(
            Guid.NewGuid(),
            1,
            response,
            " "));
        Assert.Throws<ArgumentException>(() => new WorkflowAgentStartInput(
            Guid.Empty,
            WorkflowAgentDefinitionSelectionMode.LatestActive));
        Assert.Throws<ArgumentException>(() => new WorkflowAgentStartInput(
            Guid.NewGuid(),
            WorkflowAgentDefinitionSelectionMode.ExactSavedVersion));
        Assert.Throws<ArgumentException>(() => new WorkflowAgentStartInput(
            Guid.NewGuid(),
            WorkflowAgentDefinitionSelectionMode.LatestActive,
            Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new WorkflowAgentStartInput(
            Guid.NewGuid(),
            WorkflowAgentDefinitionSelectionMode.LatestActive,
            idempotencyKey: " "));
    }

    [Fact]
    public void ExternalResponseToolInputExposesOnlyRequestVersionPayloadAndIdempotency()
    {
        var properties = typeof(WorkflowAgentExternalResponseInput)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(property => property.Name, property => property.PropertyType, StringComparer.Ordinal);

        Assert.Equal(4, properties.Count);
        Assert.Equal(typeof(Guid), properties[nameof(WorkflowAgentExternalResponseInput.ExternalRequestId)]);
        Assert.Equal(typeof(long), properties[nameof(WorkflowAgentExternalResponseInput.ExpectedRequestVersion)]);
        Assert.Equal(typeof(JsonElement), properties[nameof(WorkflowAgentExternalResponseInput.Response)]);
        Assert.Equal(typeof(string), properties[nameof(WorkflowAgentExternalResponseInput.IdempotencyKey)]);
        Assert.DoesNotContain(
            properties.Keys,
            name => name.Contains("Actor", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Scope", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Action", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Profile", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Capability", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ActorContextFactoryUsesGovernanceAndRechecksActiveAgentCapability()
    {
        var harness = CreateHarness();
        var governance = Assert.IsType<AgentExecutionGovernanceSnapshot>(harness.Context.Governance);
        var workspaceService = (IAgentFrameworkWorkspaceService)(object)harness.Workspace;
        var factory = new WorkflowExternalResponseActorContextFactory(
            new FixedDatabaseProfileRuntimeAccessor(governance.DatabaseProfileId),
            new FixedAgentExecutionProfileGenerationSource(governance.DatabaseProfileGeneration),
            new WorkflowAgentRuntimeAuthorizationService(workspaceService),
            TimeProvider.System);

        var actorContext = Assert.IsType<WorkflowExternalResponseActorContext.Authenticated>(
            await factory.CreateAgentAsync(harness.Context));
        var localOperator = Assert.IsType<WorkflowExternalResponseActorContext.Authenticated>(
            factory.CreateLocalOperator());

        Assert.Equal(WorkflowLaunchActorKind.Agent, actorContext.Actor.Kind);
        Assert.Equal(harness.Context.Agent.Id.ToString("D"), actorContext.Actor.SubjectId);
        Assert.Equal(WorkflowExternalResponseTrustedChannel.AgentTool, actorContext.Channel);
        Assert.Equal(
            WorkflowExternalResponseCallerCapabilities.SubmitHumanInput,
            actorContext.Access.Capabilities);
        var grantedScope = Assert.Single(actorContext.Access.GrantedScopes);
        Assert.Equal(governance.WorkspaceScope.Kind, grantedScope.Kind);
        Assert.Equal(governance.WorkspaceScope.Key, grantedScope.Key);
        Assert.Equal(
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            actorContext.Access.PolicyFingerprint);
        Assert.Equal(WorkflowLaunchActorKind.User, localOperator.Actor.Kind);
        Assert.Equal(WorkflowExternalResponseTrustedChannel.LocalOperator, localOperator.Channel);
        Assert.Equal(
            WorkflowExternalResponseCallerCapabilities.SubmitHumanInput |
            WorkflowExternalResponseCallerCapabilities.SubmitApprovalDecision,
            localOperator.Access.Capabilities);
        Assert.Equal(governance.DatabaseProfileId, localOperator.Access.DatabaseProfileId);

        harness.Workspace.Capabilities = [];

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            factory.CreateAgentAsync(harness.Context));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            factory.CreateAgentAsync(harness.Context with { Governance = null }));
    }

    [Fact]
    public async Task PageActorProviderUsesAuthenticatedSubjectOrExplicitLocalMode()
    {
        var harness = CreateHarness();
        var governance = Assert.IsType<AgentExecutionGovernanceSnapshot>(harness.Context.Governance);
        var profileAccessor = new FixedDatabaseProfileRuntimeAccessor(governance.DatabaseProfileId);
        var generationSource = new FixedAgentExecutionProfileGenerationSource(
            governance.DatabaseProfileGeneration);
        var actorFactory = new WorkflowExternalResponseActorContextFactory(
            profileAccessor,
            generationSource,
            new WorkflowAgentRuntimeAuthorizationService(
                (IAgentFrameworkWorkspaceService)(object)harness.Workspace),
            TimeProvider.System);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "authenticated-user-42"),
                new Claim("iat", DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds().ToString())
            ],
            authenticationType: "test"));
        var authenticatedProvider = new WorkflowExternalResponsePageActorContextProvider(
            new FixedAuthenticationStateProvider(principal),
            Options.Create(new ApiAccessOptions
            {
                Authorization = new ApiAuthorizationOptions { Enabled = true }
            }),
            actorFactory,
            profileAccessor,
            generationSource,
            TimeProvider.System);
        var localProvider = new WorkflowExternalResponsePageActorContextProvider(
            new FixedAuthenticationStateProvider(new ClaimsPrincipal()),
            Options.Create(new ApiAccessOptions
            {
                Authorization = new ApiAuthorizationOptions { Enabled = false }
            }),
            actorFactory,
            profileAccessor,
            generationSource,
            TimeProvider.System);

        var authenticated = Assert.IsType<WorkflowExternalResponseActorContext.Authenticated>(
            await authenticatedProvider.GetCurrentAsync());
        var local = Assert.IsType<WorkflowExternalResponseActorContext.Authenticated>(
            await localProvider.GetCurrentAsync());

        Assert.Equal("authenticated-user-42", authenticated.Actor.SubjectId);
        Assert.Equal(WorkflowExternalResponseTrustedChannel.Api, authenticated.Channel);
        Assert.Equal(WorkflowLaunchActorKind.User, authenticated.Actor.Kind);
        Assert.Equal(WorkflowExternalResponseTrustedChannel.LocalOperator, local.Channel);
        Assert.NotEqual(authenticated.Actor.SubjectId, local.Actor.SubjectId);
    }

    [Theory]
    [InlineData("src/App/CanDoItAll.Web/Api/WorkflowExternalResponseEndpoints.cs")]
    [InlineData("src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/WorkflowAgentRuntimeToolProvider.cs")]
    [InlineData("src/Modules/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs")]
    public void ExternalResponseCallerUsesCommonFacadeWithoutRawManagerBypass(string relativePath)
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("IWorkflowExternalResponseService", source, StringComparison.Ordinal);
        Assert.Contains("WorkflowExternalResponseCommand", source, StringComparison.Ordinal);
        Assert.Contains(".SubmitAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RespondToExternalRequestAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SubmitExternalResponseAsync", source, StringComparison.Ordinal);
    }

    private static RuntimeHarness CreateHarness(
        IEnumerable<string>? capabilityKeys = null,
        RecordingWorkflowCatalog? catalog = null,
        RecordingWorkflowLaunchService? launchService = null,
        RecordingWorkflowRuntimeManager? runtimeManager = null,
        RecordingWorkflowExternalResponseService? externalResponseService = null,
        AgentRuntimeToolProviderPurpose purpose = AgentRuntimeToolProviderPurpose.InteractiveChat,
        string runtimeSessionKey = "runtime-session-1",
        AgentRuntimeContextIntent? contextIntent = null)
    {
        var now = DateTimeOffset.UtcNow;
        var capabilities = (capabilityKeys ?? WorkflowAgentCapabilityKeys.Keys)
            .Select(key => new CapabilityCatalogItem(
                Guid.NewGuid(),
                CapabilityKind.Tool,
                key,
                key,
                string.Empty,
                string.Empty,
                string.Empty,
                CapabilityProofStatus.Verified,
                string.Empty,
                now,
                IsBuiltIn: true))
            .ToArray();
        var assignments = capabilities
            .Select(capability => new AgentCapabilityAssignment(
                capability.Id,
                capability.Key,
                capability.Kind,
                capability.ProofStatus,
                capability.LastVerifiedAtUtc,
                capability.ProofNotes))
            .ToArray();
        var agent = CreateAgent(assignments, now);
        var workspaceService = DispatchProxy.Create<IAgentFrameworkWorkspaceService, AuthorizationWorkspaceProxy>();
        var workspace = (AuthorizationWorkspaceProxy)(object)workspaceService;
        workspace.Agents = [agent];
        workspace.Capabilities = capabilities;
        var provider = new WorkflowAgentRuntimeToolProvider(
            catalog ?? new RecordingWorkflowCatalog(),
            launchService ?? new RecordingWorkflowLaunchService(),
            runtimeManager ?? new RecordingWorkflowRuntimeManager(),
            externalResponseService ??= new RecordingWorkflowExternalResponseService(),
            new RecordingWorkflowExternalResponseActorContextFactory(),
            new WorkflowAgentRuntimeAuthorizationService(workspaceService));
        var profileId = Guid.NewGuid();
        var generation = new DatabaseProfileGeneration(1);
        var scope = WorkspaceScopeDescriptor.Organization(profileId.ToString("N"));
        var context = new AgentRuntimeToolProviderContext(
            agent,
            CreateChatProvider(),
            capabilities,
            SuppressApprovalRequirements: false,
            purpose,
            runtimeSessionKey,
            contextIntent ?? AgentRuntimeContextIntent.Empty,
            Tags: new Dictionary<string, string>())
        {
            Governance = new AgentExecutionGovernanceSnapshot(
                AgentExecutionAuthorityId.Create(),
                agent.Id,
                profileId,
                generation,
                scope,
                readAllowed: true,
                mutationAllowed: true,
                "workflow-agent-test-v1",
                "workflow-agent-governance-test",
                allowedCapabilityKeys: capabilities.Select(item => item.Key).ToArray())
        };
        return new RuntimeHarness(provider, context, workspace, externalResponseService);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate CanDoItAll.slnx from the test output directory.");
    }

    private static async Task<AITool> GetToolAsync(
        WorkflowAgentRuntimeToolProvider provider,
        AgentRuntimeToolProviderContext context,
        string toolName)
    {
        var tools = await provider.CreateToolsAsync(context, CancellationToken.None);
        return GetTool(tools, toolName);
    }

    private static AITool GetTool(IReadOnlyList<AITool> tools, string toolName)
        => Assert.Single(tools, tool => string.Equals(tool.Name, toolName, StringComparison.Ordinal));

    private static async Task<TResult> InvokeAsync<TResult>(AITool tool, object? request = null)
    {
        var function = Assert.IsAssignableFrom<AIFunction>(tool);
        var arguments = new AIFunctionArguments();
        if (request is not null)
        {
            arguments["request"] = request;
        }

        var rawResult = await function.InvokeAsync(arguments);
        return rawResult switch
        {
            TResult result => result,
            JsonElement element => JsonSerializer.Deserialize<TResult>(element.GetRawText(), ResultJsonOptions)
                ?? throw new InvalidOperationException("Workflow agent tool returned null JSON."),
            _ => throw new InvalidOperationException(
                $"Unexpected workflow agent tool result type '{rawResult?.GetType().FullName ?? "<null>"}'.")
        };
    }

    private static void AssertMetadata(
        IReadOnlyList<AgentRuntimeToolMetadata> metadata,
        string toolName,
        AgentRuntimeToolOperationKind operationKind,
        bool requiresApproval)
    {
        var item = Assert.Single(metadata, item => item.ToolName == toolName);
        Assert.Equal(operationKind, item.OperationKind);
        Assert.Equal(requiresApproval, item.RequiresApprovalByDefault);
    }

    private static AgentRuntimeContextIntent CreateProcessIntent(string processRunId)
        => new(
            SourceKind: "process-assignment",
            SourceId: "assignment-8",
            ProcessRunId: processRunId,
            ProcessStepId: "step-3",
            TargetScope: "workflow-runtime",
            IsGovernedProcessStep: true,
            BrowserToolsAllowed: false,
            AllowsProductMutation: false,
            WorkspaceToolProfile: null,
            WorkspaceScope: null,
            AllowedOperations: [ProcessOperationContractNames.LaunchRuntime]);

    private static AgentDefinition CreateAgent(
        IReadOnlyList<AgentCapabilityAssignment> capabilities,
        DateTimeOffset now)
    {
        return new AgentDefinition(
            Guid.NewGuid(),
            "Workflow agent",
            "Workflow operator",
            "Exercises governed workflow tools.",
            "Use workflow tools deliberately.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            "gpt-5-mini",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.ProviderDefault,
            0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            capabilities,
            [],
            now,
            now);
    }

    private static ProviderProfile CreateChatProvider()
        => new(
            Guid.NewGuid(),
            "OpenAI chat",
            ProviderKind.OpenAi,
            "https://api.openai.com",
            "OPENAI_API_KEY",
            "gpt-5-mini",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: string.Empty,
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: [],
            ProviderProfilePurpose.Chat);

    private static WorkflowDefinition CreateDefinition(
        WorkflowId workflowId,
        WorkflowVersionId versionId,
        WorkflowLifecycleStatus status,
        string name)
    {
        var now = DateTimeOffset.UtcNow;
        var start = new WorkflowNode(
            new WorkflowNodeId("start"),
            WorkflowNodeKind.Start,
            "Start",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));
        return new WorkflowDefinition(
            workflowId,
            versionId,
            name,
            "Workflow definition for agent tool tests.",
            status,
            new WorkflowGraph(start.Id, [start], []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            now,
            now);
    }

    private static WorkflowRunSnapshot CreateRun(WorkflowRunState state)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkflowRunSnapshot(
            WorkflowRunId.New(),
            WorkflowId.New(),
            WorkflowVersionId.New(),
            state,
            WorkflowRuntimeBackendKind.InProcess,
            "agent-tool-test",
            state.ToString(),
            now,
            now);
    }

    private sealed record RuntimeHarness(
        WorkflowAgentRuntimeToolProvider Provider,
        AgentRuntimeToolProviderContext Context,
        AuthorizationWorkspaceProxy Workspace,
        RecordingWorkflowExternalResponseService ExternalResponses);

    private class AuthorizationWorkspaceProxy : DispatchProxy
    {
        public IReadOnlyList<AgentDefinition> Agents { get; set; } = [];

        public IReadOnlyList<CapabilityCatalogItem> Capabilities { get; set; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) =>
                    Task.FromResult(Agents),
                nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync) =>
                    Task.FromResult(Capabilities),
                _ => throw new InvalidOperationException(
                    $"Workspace service member '{targetMethod?.Name}' was not expected in this runtime-provider test.")
            };
        }
    }

    private sealed class FixedDatabaseProfileRuntimeAccessor(Guid profileId) :
        CanDoItAll.Infrastructure.ControlPlane.IDatabaseProfileRuntimeAccessor
    {
        private readonly CanDoItAll.Infrastructure.ControlPlane.ResolvedDatabaseProfile profile = new(
            new CanDoItAll.Infrastructure.ControlPlane.DatabaseProfileRecord
            {
                Id = profileId,
                DisplayName = "Workflow agent test",
                ProviderKind = CanDoItAll.Infrastructure.ControlPlane.DatabaseProviderKind.InMemory,
                SourceKind = CanDoItAll.Infrastructure.ControlPlane.DatabaseProfileSourceKind.InMemory
            },
            CanDoItAll.Infrastructure.ControlPlane.DatabaseProfileResolutionSource.ExplicitOverride,
            "test");

        public CanDoItAll.Infrastructure.ControlPlane.ResolvedDatabaseProfile ResolveCurrentProfile()
            => profile;

        public CanDoItAll.Infrastructure.ControlPlane.ResolvedDatabaseProfile ResolveProfile(Guid requestedProfileId)
            => profile;
    }

    private sealed class FixedAuthenticationStateProvider(ClaimsPrincipal principal) :
        AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class RecordingWorkflowLaunchService : IWorkflowLaunchService
    {
        public List<WorkflowLaunchIntent> Intents { get; } = [];

        public Task<WorkflowLaunchResult> LaunchAsync(
            WorkflowLaunchIntent intent,
            CancellationToken cancellationToken = default)
        {
            Intents.Add(intent);
            var (workflowId, versionId) = intent.Selection switch
            {
                WorkflowDefinitionSelection.ExactSavedVersion exact =>
                    (exact.WorkflowId, exact.VersionId),
                WorkflowDefinitionSelection.LatestActive latest =>
                    (latest.WorkflowId, WorkflowVersionId.New()),
                _ => throw new InvalidOperationException("Agent tool test launch received an unsupported selection.")
            };
            var definition = CreateDefinition(
                workflowId,
                versionId,
                WorkflowLifecycleStatus.Active,
                "Launched workflow");
            var backend = new WorkflowRuntimeBackendDescriptor(
                WorkflowRuntimeBackendKind.InProcess,
                "Test backend",
                IsDurable: false,
                SupportsStreaming: true,
                SupportsExternalRequests: true,
                SupportsDashboardObservability: false,
                OperationalNotes: "Test backend.");
            var resolved = new WorkflowResolvedRuntimeRequest(
                definition,
                intent.InputJson,
                backend,
                intent.PreviewSimulationPlan,
                intent.Mode,
                intent.Origin,
                intent.CompletionPolicy,
                intent.Idempotency,
                DateTimeOffset.UtcNow);
            var run = CreateRun(WorkflowRunState.Completed) with
            {
                WorkflowId = workflowId,
                VersionId = versionId
            };
            return Task.FromResult(new WorkflowLaunchResult(
                run,
                resolved,
                intent.Idempotency is WorkflowLaunchIdempotency.CallerSupplied
                    ? WorkflowLaunchIdempotencyDisposition.EnforcedNewRun
                    : WorkflowLaunchIdempotencyDisposition.NotRequested));
        }
    }

    private sealed class RecordingWorkflowRuntimeManager : IWorkflowRuntimeManager
    {
        public WorkflowRunSnapshot? Run { get; set; }

        public WorkflowRunCancellationResult CancellationResult { get; set; } = new(
            WorkflowRunCancellationOutcome.NotFound,
            Run: null,
            "Run was not found.");

        public Task<WorkflowRunSnapshot?> GetRunAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Run?.RunId == runId ? Run : null);

        public Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
            WorkflowId? workflowId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowRunSnapshot>>(
                Run is not null && (workflowId is null || Run.WorkflowId == workflowId)
                    ? [Run]
                    : []);

        public Task<WorkflowRunCancellationResult> RequestCancellationAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CancellationResult);

        public Task<WorkflowRunSnapshot> StartAsync(
            WorkflowDefinition definition,
            WorkflowRunStartRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowEventRecord>>([]);

        public Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowCheckpointRecord>>([]);

        public Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
            WorkflowEventPageRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new WorkflowListPage<WorkflowEventRecord>([], 0, request.PageSize, 0));

        public Task<WorkflowRunSnapshot> CancelAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

    }

    private sealed class RecordingWorkflowExternalResponseService : IWorkflowExternalResponseService
    {
        public WorkflowExternalResponseCommand? Command { get; private set; }

        public WorkflowExternalResponseServiceResult Result { get; set; } = new(
            WorkflowExternalResponseServiceOutcome.RequestNotFound,
            Operation: null,
            Run: null,
            Request: null,
            NextRequest: null,
            Replayed: false,
            "Request was not found.");

        public Task<WorkflowExternalResponseServiceResult> SubmitAsync(
            WorkflowExternalResponseCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(Result);
        }

        public Task<WorkflowExternalResponseServiceResult> GetStatusAsync(
            WorkflowExternalResponseStatusQuery query,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingWorkflowExternalResponseActorContextFactory :
        IWorkflowExternalResponseActorContextFactory
    {
        public WorkflowExternalResponseActorContext CreateLocalOperator()
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseActorContext> CreateAgentAsync(
            AgentRuntimeToolProviderContext context,
            CancellationToken cancellationToken = default)
        {
            var governance = context.Governance
                ?? throw new InvalidOperationException("Test agent governance is required.");
            var profileScope = WorkspaceScopeDescriptor.Organization(
                governance.DatabaseProfileId.ToString("N"));
            WorkflowExternalResponseActorContext actorContext =
                new WorkflowExternalResponseActorContext.Authenticated(
                    new WorkflowLaunchActor(
                        WorkflowLaunchActorKind.Agent,
                        context.Agent.Id.ToString("D")),
                    WorkflowExternalResponseTrustedChannel.AgentTool,
                    new WorkflowExternalResponseCallerAccess(
                        governance.DatabaseProfileId,
                        governance.DatabaseProfileGeneration,
                        profileScope,
                        [governance.WorkspaceScope],
                        WorkflowExternalResponseCallerCapabilities.SubmitHumanInput,
                        WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                        DateTimeOffset.UtcNow));
            return Task.FromResult(actorContext);
        }
    }

    private sealed class RecordingWorkflowCatalog : IWorkflowCatalogService
    {
        public IReadOnlyList<WorkflowCatalogItem> Items { get; init; } = [];

        public Dictionary<WorkflowId, WorkflowDefinitionDetail> ActiveDefinitions { get; } = [];

        public Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(Items);

        public Task<WorkflowDefinitionDetail?> GetLatestDefinitionByStatusAsync(
            WorkflowId workflowId,
            WorkflowLifecycleStatus status,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                status == WorkflowLifecycleStatus.Active &&
                ActiveDefinitions.TryGetValue(workflowId, out var detail)
                    ? detail
                    : null);

        public Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinition> SaveDefinitionAsync(
            WorkflowDefinitionSaveRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinition> ChangeDefinitionStatusAsync(
            WorkflowDefinitionStatusChangeRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinitionExportEnvelope?> ExportDefinitionAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinition> ImportDefinitionAsync(
            WorkflowDefinitionImportRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteDefinitionAsync(
            WorkflowId workflowId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowValidationResult> ValidateDefinitionAsync(
            WorkflowDefinition definition,
            CancellationToken cancellationToken = default)
            => Task.FromResult(WorkflowValidationResult.Success);
    }
}
