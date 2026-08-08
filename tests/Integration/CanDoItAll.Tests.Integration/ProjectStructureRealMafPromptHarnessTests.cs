using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureRealMafPromptHarnessTests
{
    private const string UnauthorizedSentinelAssetTitle = "Authority-negative sentinel asset";

    private static readonly byte[] BaselineAssetBytes = Encoding.UTF8.GetBytes(
        "# Acceptance baseline\n\nThis content hash must remain exact.\n");

    private static readonly byte[] CopyAssetBytes = Encoding.UTF8.GetBytes(
        "# Real MAF copy proof\n\nThe copied managed content must remain byte-identical.\n");

    private static readonly JsonSerializerOptions ApiJsonOptions = CreateApiJsonOptions();

    [Fact]
    public async Task Scripted_transport_exercises_real_maf_project_structure_tool_loop_but_is_not_provider_transport_proof()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
            "project-structure-real-maf-prompt-harness",
            environment => environment.CreatePostgreSqlProfile("real-maf-prompt-harness"),
            services =>
            {
                services.AddSingleton<ScriptedProjectStructureChatClient>();
                services.Replace(
                    ServiceDescriptor.Singleton<IMafProviderAgentFactory, ScriptedProjectStructureMafProviderAgentFactory>());
            });
        var project = await CreateProjectAsync(host.Client);
        var parentNode = await CreateParentNodeAsync(host.Client, project.Id);
        var baselineAsset = await CreateBaselineAssetAsync(host.Client, project.Id, parentNode.Id);
        var hierarchyFixture = await CreateHierarchyFixtureAsync(host.App.Services, project.Id);
        var canonicalGraphBefore = await CaptureAcceptanceGraphAsync(host.App.Services, project.Id);
        var capturedAsset = Assert.Single(
            canonicalGraphBefore.ManagedAssets,
            asset => string.Equals(asset.NodeId, baselineAsset.Id, StringComparison.Ordinal));
        Assert.Equal(BaselineAssetBytes.LongLength, capturedAsset.ContentLength);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(BaselineAssetBytes)),
            capturedAsset.Sha256);
        Assert.Equal("text/markdown", capturedAsset.MediaContentType);
        Assert.Equal("acceptance-baseline.md", capturedAsset.MediaOriginalFileName);
        Assert.Contains(hierarchyFixture.RelatedEdge, canonicalGraphBefore.HierarchyEdges);
        Assert.DoesNotContain(hierarchyFixture.UnrelatedEdge, canonicalGraphBefore.HierarchyEdges);

        await using var scope = host.App.Services.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();
        var scriptedClient = host.App.Services.GetRequiredService<ScriptedProjectStructureChatClient>();
        scriptedClient.ConfigureScenario(project.Id, parentNode.Id);
        var agentId = await CreateAgentAsync(workspaceService, project.Id);
        var contextMetadata = ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
            "{}",
            WorkspaceScopeDescriptor.Project(project.Id.ToString("D")));

        using var executionResponse = await host.Client.PostAsJsonAsync(
            $"/api/agents/{agentId:D}/execution-runs",
            new
            {
                prompt = "Create one architecture node under the selected parent, verify it canonically, and release the lease.",
                context = new ExecutionInvocationContext(
                    SourceKind: ProjectStructureAgentChatContextBuilder.SourceKind,
                    SourceId: project.Id.ToString("D"),
                    CorrelationId: Guid.NewGuid().ToString("N"),
                    CausationId: string.Empty,
                    RequestedBy: "integration-test",
                    RequestedByKind: "test",
                    MetadataJson: contextMetadata),
                autoApprovePendingToolCalls = true
            });
        var executionBody = await executionResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, executionResponse.StatusCode);
        var executionResult = JsonSerializer.Deserialize<ExecutionRunResult>(executionBody, ApiJsonOptions)
            ?? throw new InvalidOperationException("The execution endpoint returned no result.");
        Assert.Equal(ExecutionState.Completed, executionResult.State);
        Assert.Equal("Deterministic Project Structure tool loop completed.", executionResult.ResponseText);

        Assert.Equal(3, scriptedClient.InvocationCount);
        Assert.False(string.IsNullOrWhiteSpace(scriptedClient.FactoryProviderName));
        Assert.False(string.IsNullOrWhiteSpace(scriptedClient.FactoryModel));
        Assert.Equal(
            [
                ScriptedProjectStructureChatClient.NodeCreateToolName,
                ScriptedProjectStructureChatClient.StructureReadToolName
            ],
            scriptedClient.IssuedToolNames);
        Assert.All(
            scriptedClient.CapturedToolNames,
            toolNames =>
            {
                Assert.Contains(ScriptedProjectStructureChatClient.NodeCreateToolName, toolNames);
                Assert.Contains(ScriptedProjectStructureChatClient.StructureReadToolName, toolNames);
                Assert.DoesNotContain(ScriptedProjectStructureChatClient.ProjectLeaseAcquireToolName, toolNames);
                Assert.DoesNotContain(ScriptedProjectStructureChatClient.LeaseReleaseToolName, toolNames);
            });

        var derivedNode = Assert.IsType<ProjectStructureNodeSummary>(scriptedClient.CreatedNode);
        var toolReadback = Assert.IsType<ProjectStructureReadToolData>(scriptedClient.CanonicalReadback);
        var toolReadbackNode = Assert.Single(toolReadback.Nodes);
        Assert.Equal(derivedNode.Id, toolReadbackNode.Id);
        Assert.Equal(parentNode.Id, toolReadbackNode.ParentId);
        Assert.Equal("Deterministic MAF child", toolReadbackNode.Title);
        Assert.Equal(ProjectObjectType.ProjectBlock, toolReadbackNode.ObjectType);

        var canonicalReadback = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure/projects/{project.Id:D}/structure/read",
            new ProjectStructureReadRequest(
                NodeIds: [derivedNode.Id],
                IncludeMetadata: true,
                IncludeNotes: true,
                Source: ProjectStructureReadSource.CanonicalCurrent));
        var canonicalNode = Assert.Single(canonicalReadback.Nodes);
        Assert.Equal(parentNode.Id, canonicalNode.ParentId);
        Assert.Equal("Deterministic MAF child", canonicalNode.Title);
        Assert.Equal(ProjectObjectType.ProjectBlock, canonicalNode.ObjectType);
        Assert.Equal("architecture", canonicalNode.ObjectSubtype);

        // Success facts (exit summary, provider key) are internal acceptance
        // evidence read through the workspace service; the public receipts API
        // deliberately exposes only the privacy-safe projection.
        using var receiptsResponse = await host.Client.GetAsync(
            $"/api/agents/{agentId:D}/execution-runs/{executionResult.ExecutionRunId:D}/tool-receipts");
        Assert.Equal(HttpStatusCode.OK, receiptsResponse.StatusCode);
        var receipts = (await workspaceService.ListToolExecutionReceiptsAsync(
            executionResult.ExecutionRunId)).ToList();
        var expectedReceiptNames = scriptedClient.IssuedToolNames.ToHashSet(StringComparer.Ordinal);
        var projectStructureReceipts = receipts
            .Where(receipt => expectedReceiptNames.Contains(receipt.ToolName))
            .ToArray();
        Assert.Equal(expectedReceiptNames.Count, projectStructureReceipts.Length);
        Assert.All(projectStructureReceipts, receipt =>
        {
            Assert.Equal("Succeeded", receipt.ExitSummary);
            Assert.Equal("project-structure.runtime-tools", receipt.RuntimeToolProviderKey);
        });

        // Implicit mutation lease: acquired and released inside the mutation
        // tool; nothing may remain active afterwards.
        Assert.Null(await leaseService.GetActiveLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            project.Id.ToString("D")));

        var canonicalGraphAfter = await CaptureAcceptanceGraphAsync(host.App.Services, project.Id);
        var expectedCreatedNode = ToAcceptanceNodeSnapshot(derivedNode);
        var expectedCreatedLink = new ProjectStructureCanonicalLinkSnapshot(
            parentNode.Id,
            expectedCreatedNode.Id,
            ProjectObjectLinkKind.BelongsTo,
            false);
        var requiredToolNames = scriptedClient.IssuedToolNames.ToArray();
        var toolManifest = scriptedClient.CapturedToolNames
            .SelectMany(toolNames => toolNames)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var acceptanceReceipts = receipts
            .Select(receipt => new ProjectStructureAcceptanceToolReceipt(
                receipt.ToolName,
                string.Equals(receipt.ExitSummary, "Succeeded", StringComparison.Ordinal)
                    ? ProjectStructureAcceptanceReceiptOutcome.Succeeded
                    : ProjectStructureAcceptanceReceiptOutcome.Failed))
            .ToArray();
        var acceptanceContract = new ProjectStructureAcceptanceContract(
            requiredToolNames,
            canonicalGraphBefore,
            new ProjectStructureCanonicalAllowedDelta([expectedCreatedNode], [])
            {
                UpsertedLinks = [expectedCreatedLink]
            },
            parentNode.Id);
        var acceptanceEvidence = new ProjectStructureAcceptanceEvidence(
            executionResult.State,
            executionResult.ResponseText,
            toolManifest,
            acceptanceReceipts,
            canonicalGraphAfter);

        var acceptanceDecision = ProjectStructureAgentAcceptanceOracle.Evaluate(
            acceptanceContract,
            acceptanceEvidence);

        Assert.True(
            acceptanceDecision.IsAccepted,
            $"Real MAF evidence was rejected: {JsonSerializer.Serialize(acceptanceDecision.Rejections, ApiJsonOptions)}");

        var tamperedEvidence = acceptanceEvidence with
        {
            CanonicalGraphAfter = canonicalGraphAfter with
            {
                Nodes = canonicalGraphAfter.Nodes
                    .Select(node => string.Equals(node.Id, parentNode.Id, StringComparison.Ordinal)
                        ? node with { Title = "Unauthorized collateral mutation" }
                        : node)
                    .ToArray()
            }
        };
        var tamperedDecision = ProjectStructureAgentAcceptanceOracle.Evaluate(
            acceptanceContract,
            tamperedEvidence);

        var tamperedRejection = Assert.Single(tamperedDecision.Rejections);
        Assert.False(tamperedDecision.IsAccepted);
        Assert.Equal(Sb01ProjectStructureInvariantIds.CanonicalSentinelMustRemainUnchanged, tamperedRejection.InvariantId);
        Assert.Equal(ProjectStructureAcceptanceFailure.CanonicalSentinelDrifted, tamperedRejection.Failure);

        var assetTamperedDecision = ProjectStructureAgentAcceptanceOracle.Evaluate(
            acceptanceContract,
            acceptanceEvidence with
            {
                CanonicalGraphAfter = canonicalGraphAfter with
                {
                    ManagedAssets = canonicalGraphAfter.ManagedAssets
                        .Select(asset => string.Equals(asset.NodeId, baselineAsset.Id, StringComparison.Ordinal)
                            ? asset with { Sha256 = new string('A', 64) }
                            : asset)
                        .ToArray()
                }
            });

        var assetTamperedRejection = Assert.Single(assetTamperedDecision.Rejections);
        Assert.Equal(
            ProjectStructureAcceptanceFailure.CanonicalBaselineAssetDrifted,
            assetTamperedRejection.Failure);
        Assert.Equal(
            Sb01ProjectStructureInvariantIds.CanonicalAllowedDeltaMustBeExact,
            assetTamperedRejection.InvariantId);
    }

    [Fact]
    public async Task Scripted_transport_executes_real_maf_copy_loop_with_exact_canonical_delta()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
            "project-structure-real-maf-copy-harness",
            environment => environment.CreatePostgreSqlProfile("real-maf-copy-harness"),
            services =>
            {
                services.AddSingleton<ScriptedProjectStructureChatClient>();
                services.Replace(
                    ServiceDescriptor.Singleton<IMafProviderAgentFactory, ScriptedProjectStructureMafProviderAgentFactory>());
            });
        var project = await CreateProjectAsync(host.Client);
        var fixture = await CreateCopyFixtureAsync(host.App.Services, project.Id);
        var canonicalGraphBefore = await CaptureAcceptanceGraphAsync(host.App.Services, project.Id);
        var sourceRootBefore = Assert.Single(
            canonicalGraphBefore.Nodes,
            node => string.Equals(node.Id, fixture.SourceRoot.Id, StringComparison.Ordinal));
        var sourceAssetBefore = Assert.Single(
            canonicalGraphBefore.Nodes,
            node => string.Equals(node.Id, fixture.SourceAsset.Id, StringComparison.Ordinal));
        var sourceManagedAssetBefore = Assert.Single(
            canonicalGraphBefore.ManagedAssets,
            asset => string.Equals(asset.NodeId, fixture.SourceAsset.Id, StringComparison.Ordinal));
        Assert.Equal(CopyAssetBytes.LongLength, sourceManagedAssetBefore.ContentLength);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(CopyAssetBytes)), sourceManagedAssetBefore.Sha256);

        await using var scope = host.App.Services.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();
        var scriptedClient = host.App.Services.GetRequiredService<ScriptedProjectStructureChatClient>();
        scriptedClient.ConfigureCopyScenario(
            project.Id,
            [fixture.SourceAsset.Id, fixture.SourceRoot.Id],
            fixture.Destination.Id);
        var agentId = await CreateAgentAsync(workspaceService, project.Id);
        var contextMetadata = ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
            "{}",
            WorkspaceScopeDescriptor.Project(project.Id.ToString("D")));

        using var executionResponse = await host.Client.PostAsJsonAsync(
            $"/api/agents/{agentId:D}/execution-runs",
            new
            {
                prompt = "Copy the selected source subtree under the explicit destination, read back every authoritative copied id, and release the lease.",
                context = new ExecutionInvocationContext(
                    SourceKind: ProjectStructureAgentChatContextBuilder.SourceKind,
                    SourceId: project.Id.ToString("D"),
                    CorrelationId: Guid.NewGuid().ToString("N"),
                    CausationId: string.Empty,
                    RequestedBy: "integration-test",
                    RequestedByKind: "test",
                    MetadataJson: contextMetadata),
                autoApprovePendingToolCalls = true
            });
        var executionBody = await executionResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, executionResponse.StatusCode);
        var executionResult = JsonSerializer.Deserialize<ExecutionRunResult>(executionBody, ApiJsonOptions)
            ?? throw new InvalidOperationException("The execution endpoint returned no result.");
        Assert.Equal(ExecutionState.Completed, executionResult.State);
        Assert.Equal("Deterministic Project Structure tool loop completed.", executionResult.ResponseText);
        Assert.Equal(3, scriptedClient.InvocationCount);
        Assert.Equal(
            [
                ScriptedProjectStructureChatClient.NodesCopyToolName,
                ScriptedProjectStructureChatClient.StructureReadToolName
            ],
            scriptedClient.IssuedToolNames);
        Assert.All(
            scriptedClient.CapturedToolNames,
            toolNames =>
            {
                Assert.Contains(ScriptedProjectStructureChatClient.NodesCopyToolName, toolNames);
                Assert.Contains(ScriptedProjectStructureChatClient.StructureReadToolName, toolNames);
                Assert.DoesNotContain(ScriptedProjectStructureChatClient.ProjectLeaseAcquireToolName, toolNames);
                Assert.DoesNotContain(ScriptedProjectStructureChatClient.LeaseReleaseToolName, toolNames);
            });

        var copyResult = Assert.IsType<ProjectStructureNodesCopyResult>(scriptedClient.CopyResult);
        Assert.Equal(project.Id, copyResult.ProjectId);
        Assert.Equal(fixture.Destination.Id, copyResult.DestinationParentNodeId);
        Assert.Equal([fixture.SourceRoot.Id], copyResult.SourceRootNodeIds);
        Assert.Equal(2, copyResult.CopiedNodeCount);
        Assert.Equal(fixture.ExpectedOmittedBoundaryLinks, copyResult.OmittedBoundaryLinks);
        var copiedRootId = Assert.Single(
            copyResult.NodeMappings,
            mapping => string.Equals(mapping.SourceNodeId, fixture.SourceRoot.Id, StringComparison.Ordinal)).CopiedNodeId;
        var copiedAssetId = Assert.Single(
            copyResult.NodeMappings,
            mapping => string.Equals(mapping.SourceNodeId, fixture.SourceAsset.Id, StringComparison.Ordinal)).CopiedNodeId;
        Assert.Equal([copiedRootId], copyResult.CopiedRootNodeIds);
        Assert.Equal(
            new[] { fixture.SourceAsset.Id, fixture.SourceRoot.Id }
                .Order(StringComparer.Ordinal)
                .ToArray(),
            copyResult.NodeMappings.Select(mapping => mapping.SourceNodeId).ToArray());

        var canonicalToolReadback = Assert.IsType<ProjectStructureReadToolData>(scriptedClient.CanonicalReadback);
        var copiedNodeIds = copyResult.NodeMappings
            .Select(mapping => mapping.CopiedNodeId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(project.Id, canonicalToolReadback.ProjectId);
        Assert.Equal(ProjectStructureReadSource.CanonicalCurrent, canonicalToolReadback.Source);
        Assert.Equal(
            copiedNodeIds,
            canonicalToolReadback.Nodes
                .Select(node => node.Id)
                .Order(StringComparer.Ordinal)
                .ToArray());

        // Success facts (exit summary, provider key) are internal acceptance
        // evidence read through the workspace service; the public receipts API
        // deliberately exposes only the privacy-safe projection.
        using var receiptsResponse = await host.Client.GetAsync(
            $"/api/agents/{agentId:D}/execution-runs/{executionResult.ExecutionRunId:D}/tool-receipts");
        Assert.Equal(HttpStatusCode.OK, receiptsResponse.StatusCode);
        var receipts = (await workspaceService.ListToolExecutionReceiptsAsync(
            executionResult.ExecutionRunId)).ToList();
        var requiredToolNames = scriptedClient.IssuedToolNames.ToArray();
        var requiredToolNameSet = requiredToolNames.ToHashSet(StringComparer.Ordinal);
        var projectStructureReceipts = receipts
            .Where(receipt => requiredToolNameSet.Contains(receipt.ToolName))
            .ToArray();
        Assert.Equal(requiredToolNameSet.Count, projectStructureReceipts.Length);
        Assert.All(projectStructureReceipts, receipt =>
        {
            Assert.NotEqual(Guid.Empty, receipt.Id);
            Assert.Equal(executionResult.ExecutionRunId, receipt.ExecutionRunId);
            Assert.Equal("Succeeded", receipt.ExitSummary);
            Assert.Equal("PolicyEnforced", receipt.ApprovalMode);
            Assert.Equal("project-structure.runtime-tools", receipt.RuntimeToolProviderKey);
            Assert.True(receipt.CompletedAtUtc >= receipt.StartedAtUtc);
        });
        var copyReceipt = Assert.Single(
            projectStructureReceipts,
            receipt => string.Equals(
                receipt.ToolName,
                ScriptedProjectStructureChatClient.NodesCopyToolName,
                StringComparison.Ordinal));
        Assert.Equal("RuntimeProvider:Mutation", copyReceipt.RiskClass);

        // Implicit mutation lease: acquired and released inside the mutation
        // tool; nothing may remain active afterwards.
        Assert.Null(await leaseService.GetActiveLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            project.Id.ToString("D")));

        var sourceRootX = sourceRootBefore.X
            ?? throw new InvalidOperationException("The canonical source root has no X position.");
        var sourceRootY = sourceRootBefore.Y
            ?? throw new InvalidOperationException("The canonical source root has no Y position.");
        var sourceAssetX = sourceAssetBefore.X
            ?? throw new InvalidOperationException("The canonical source asset has no X position.");
        var sourceAssetY = sourceAssetBefore.Y
            ?? throw new InvalidOperationException("The canonical source asset has no Y position.");
        var deltaX = fixture.ExpectedCopiedRootPosition.X - sourceRootX;
        var deltaY = fixture.ExpectedCopiedRootPosition.Y - sourceRootY;
        var expectedCopiedRoot = sourceRootBefore with
        {
            Id = copiedRootId,
            ParentId = fixture.Destination.Id,
            X = fixture.ExpectedCopiedRootPosition.X,
            Y = fixture.ExpectedCopiedRootPosition.Y
        };
        var expectedCopiedAsset = sourceAssetBefore with
        {
            Id = copiedAssetId,
            ParentId = copiedRootId,
            X = sourceAssetX + deltaX,
            Y = sourceAssetY + deltaY
        };
        var expectedCopiedManagedAsset = sourceManagedAssetBefore with
        {
            NodeId = copiedAssetId
        };
        ProjectStructureCanonicalLinkSnapshot[] expectedCopiedLinks =
        [
            new(fixture.Destination.Id, copiedRootId, ProjectObjectLinkKind.BelongsTo, false),
            new(copiedRootId, copiedAssetId, ProjectObjectLinkKind.BelongsTo, false),
            new(copiedRootId, copiedAssetId, ProjectObjectLinkKind.Uses, true)
        ];
        var canonicalGraphAfter = await CaptureAcceptanceGraphAsync(host.App.Services, project.Id);
        var acceptanceContract = new ProjectStructureAcceptanceContract(
            requiredToolNames,
            canonicalGraphBefore,
            new ProjectStructureCanonicalAllowedDelta(
                [expectedCopiedRoot, expectedCopiedAsset],
                [])
            {
                UpsertedLinks = expectedCopiedLinks,
                UpsertedManagedAssets = [expectedCopiedManagedAsset]
            },
            fixture.Destination.Id);
        var acceptanceEvidence = new ProjectStructureAcceptanceEvidence(
            executionResult.State,
            executionResult.ResponseText,
            scriptedClient.CapturedToolNames
                .SelectMany(toolNames => toolNames)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            receipts
                .Select(receipt => new ProjectStructureAcceptanceToolReceipt(
                    receipt.ToolName,
                    string.Equals(receipt.ExitSummary, "Succeeded", StringComparison.Ordinal)
                        ? ProjectStructureAcceptanceReceiptOutcome.Succeeded
                        : ProjectStructureAcceptanceReceiptOutcome.Failed))
                .ToArray(),
            canonicalGraphAfter);
        var acceptanceDecision = ProjectStructureAgentAcceptanceOracle.Evaluate(
            acceptanceContract,
            acceptanceEvidence);

        Assert.True(
            acceptanceDecision.IsAccepted,
            $"Real MAF copy evidence was rejected: {JsonSerializer.Serialize(acceptanceDecision.Rejections, ApiJsonOptions)}");
        Assert.Equal(
            expectedCopiedManagedAsset,
            Assert.Single(canonicalGraphAfter.ManagedAssets, asset => asset.NodeId == copiedAssetId));
    }

    [Fact]
    public async Task Read_only_artifact_agent_has_no_project_structure_mutation_tools_and_preserves_the_canonical_graph()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
            "project-structure-read-only-real-maf-prompt-harness",
            environment => environment.CreatePostgreSqlProfile("read-only-real-maf-prompt-harness"),
            services =>
            {
                services.AddSingleton<ScriptedReadOnlyProjectStructureChatClient>();
                services.Replace(
                    ServiceDescriptor.Singleton<IMafProviderAgentFactory, ScriptedReadOnlyProjectStructureMafProviderAgentFactory>());
            });
        var project = await CreateProjectAsync(host.Client);
        await CreateParentNodeAsync(host.Client, project.Id);
        var graphBefore = await ReadCanonicalGraphAsync(host.App.Services, project.Id);
        var graphHashBefore = ComputeCanonicalGraphHash(graphBefore);
        Assert.DoesNotContain(
            graphBefore.Nodes,
            node => string.Equals(node.Title, UnauthorizedSentinelAssetTitle, StringComparison.Ordinal));

        await using var scope = host.App.Services.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var imageProvider = ResolveProductionSeededImageProvider(await workspaceService.ListProvidersAsync());
        var scriptedClient = host.App.Services.GetRequiredService<ScriptedReadOnlyProjectStructureChatClient>();
        scriptedClient.ConfigureScenario(project.Id);
        var agentId = await CreateReadOnlyArtifactAgentAsync(
            workspaceService,
            project.Id,
            imageProvider);
        var contextMetadata = ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
            "{}",
            WorkspaceScopeDescriptor.Project(project.Id.ToString("D")));

        using var executionResponse = await host.Client.PostAsJsonAsync(
            $"/api/agents/{agentId:D}/execution-runs",
            new
            {
                prompt = $"Read the canonical graph, then attach an image artifact named '{UnauthorizedSentinelAssetTitle}' only if the Project Structure write tool is attached. Report the missing authority explicitly and never claim a mutation without a receipt.",
                context = new ExecutionInvocationContext(
                    SourceKind: ProjectStructureAgentChatContextBuilder.SourceKind,
                    SourceId: project.Id.ToString("D"),
                    CorrelationId: Guid.NewGuid().ToString("N"),
                    CausationId: string.Empty,
                    RequestedBy: "integration-test",
                    RequestedByKind: "test",
                    MetadataJson: contextMetadata),
                autoApprovePendingToolCalls = true
            });
        var executionBody = await executionResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, executionResponse.StatusCode);
        var executionResult = JsonSerializer.Deserialize<ExecutionRunResult>(executionBody, ApiJsonOptions)
            ?? throw new InvalidOperationException("The execution endpoint returned no result.");
        Assert.Equal(ExecutionState.Completed, executionResult.State);
        Assert.Equal(ScriptedReadOnlyProjectStructureChatClient.CompletionText, executionResult.ResponseText);

        Assert.Equal(2, scriptedClient.InvocationCount);
        Assert.False(string.IsNullOrWhiteSpace(scriptedClient.FactoryProviderName));
        Assert.False(string.IsNullOrWhiteSpace(scriptedClient.FactoryModel));
        Assert.Equal(
            [ScriptedReadOnlyProjectStructureChatClient.StructureReadToolName],
            scriptedClient.IssuedToolNames);
        Assert.All(
            scriptedClient.CapturedToolNames,
            toolNames =>
            {
                Assert.Contains(ScriptedReadOnlyProjectStructureChatClient.StructureReadToolName, toolNames);
                Assert.Contains(AgentToolInvocationPolicyMetadata.ImageGenerationCreate, toolNames);
                Assert.DoesNotContain(AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate, toolNames);
                Assert.DoesNotContain(
                    AgentToolInvocationPolicyMetadata.ProjectStructureMutationTools,
                    toolNames.Contains);
            });

        var toolReadback = Assert.IsType<ProjectStructureReadToolData>(scriptedClient.CanonicalReadback);
        Assert.Equal(project.Id, toolReadback.ProjectId);
        Assert.Equal(ProjectStructureReadSource.CanonicalCurrent, toolReadback.Source);

        // Success facts (exit summary, provider key) are internal acceptance
        // evidence read through the workspace service; the public receipts API
        // deliberately exposes only the privacy-safe projection.
        using var receiptsResponse = await host.Client.GetAsync(
            $"/api/agents/{agentId:D}/execution-runs/{executionResult.ExecutionRunId:D}/tool-receipts");
        Assert.Equal(HttpStatusCode.OK, receiptsResponse.StatusCode);
        var receipts = (await workspaceService.ListToolExecutionReceiptsAsync(
            executionResult.ExecutionRunId)).ToList();
        var projectStructureReceipts = receipts
            .Where(receipt => receipt.ToolName.StartsWith("project_structure_", StringComparison.Ordinal))
            .ToArray();
        var readReceipt = Assert.Single(projectStructureReceipts);
        Assert.Equal(ScriptedReadOnlyProjectStructureChatClient.StructureReadToolName, readReceipt.ToolName);
        Assert.Equal("runtime-provider", readReceipt.ToolFamily);
        Assert.Equal("RuntimeProvider:Read", readReceipt.RiskClass);
        Assert.Equal("Succeeded", readReceipt.ExitSummary);
        Assert.Equal("project-structure.runtime-tools", readReceipt.RuntimeToolProviderKey);
        Assert.DoesNotContain(
            receipts,
            receipt => AgentToolInvocationPolicyMetadata.ProjectStructureMutationTools.Contains(
                receipt.ToolName,
                StringComparer.Ordinal));

        var graphAfter = await ReadCanonicalGraphAsync(host.App.Services, project.Id);
        var graphHashAfter = ComputeCanonicalGraphHash(graphAfter);
        Assert.Equal(graphHashBefore, graphHashAfter);
        Assert.DoesNotContain(
            graphAfter.Nodes,
            node => string.Equals(node.Title, UnauthorizedSentinelAssetTitle, StringComparison.Ordinal));
    }

    private static async Task<Guid> CreateAgentAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        Guid projectId)
    {
        var provider = (await workspaceService.ListProvidersAsync())
            .First(item => item.IsEnabled && item.SupportsTools && item.Purpose == ProviderProfilePurpose.Chat);
        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "Deterministic Project Structure MAF Agent";
        editor.RoleTitle = "Project Structure integration agent";
        editor.Summary = "Exercises real MAF Project Structure tool composition with a deterministic transport.";
        editor.Instructions = "Use the exact Project Structure tools requested by the user and verify canonical persistence.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.ProviderProfileId = provider.Id;
        editor.Model = provider.DefaultModel;
        editor.ConfigurationJson = "{}";
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;
        editor.Permissions = AgentPermissionsPolicy.Default with
        {
            AutoApproveExternalCallsByDefault = true
        };
        editor.ProjectStructureAccess = new AgentProjectStructureAccessSettings
        {
            CanRead = true,
            CanWrite = true,
            CanWriteNonTaskStructure = true,
            AllowedProjectIds = [projectId]
        };
        return await workspaceService.SaveAgentAsync(editor);
    }

    private static async Task<Guid> CreateReadOnlyArtifactAgentAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        Guid projectId,
        ProviderProfile imageProvider)
    {
        var provider = (await workspaceService.ListProvidersAsync())
            .First(item => item.IsEnabled && item.SupportsTools && item.Purpose == ProviderProfilePurpose.Chat);
        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "Read-only artifact Project Structure MAF Agent";
        editor.RoleTitle = "Read-only Project Structure artifact analyst";
        editor.Summary = "Exercises artifact tools while Project Structure mutation authority remains absent.";
        editor.Instructions = "Read the canonical Project Structure and report unavailable write authority explicitly. Never claim mutation without a successful receipt.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.ProviderProfileId = provider.Id;
        editor.Model = provider.DefaultModel;
        editor.ConfigurationJson = "{}";
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;
        editor.Permissions = AgentPermissionsPolicy.Default with
        {
            AutoApproveExternalCallsByDefault = true
        };
        editor.ProjectStructureAccess = new AgentProjectStructureAccessSettings
        {
            CanRead = true,
            CanWrite = false,
            CanWriteNonTaskStructure = false,
            CanWriteTasks = false,
            CanCreateProjects = false,
            CanCreateSubprojects = false,
            AllowedProjectIds = [projectId]
        };
        editor.WorkspaceToolAccess = AgentWorkspaceToolAccessProfiles.CreateSettings(
            AgentWorkspaceToolProfileKind.BusinessAnalysis);
        editor.ImageGenerationAccess = new AgentImageGenerationAccessSettings
        {
            CanGenerateImages = true,
            PreferredProviderProfileId = imageProvider.Id,
            DefaultModel = imageProvider.DefaultModel,
            CanStoreImagesAsProjectAssets = true
        };
        return await workspaceService.SaveAgentAsync(editor);
    }

    private static ProviderProfile ResolveProductionSeededImageProvider(
        IReadOnlyList<ProviderProfile> providers)
    {
        var provider = providers.SingleOrDefault(
            item => item.IsEnabled &&
                    item.Kind == ProviderKind.OpenAi &&
                    item.Purpose == ProviderProfilePurpose.ImageGeneration &&
                    string.Equals(item.Name, "OpenAI image generation", StringComparison.Ordinal));
        if (provider is null)
        {
            throw new InvalidOperationException(
                "Isolated-host limitation: no enabled production-seeded OpenAI image-generation provider is available, so image_generation_create attachment cannot be asserted without faking provider availability.");
        }

        var featureMatrix = new ProviderProfileService().ResolveFeatureMatrix(provider);
        if (!featureMatrix.SupportsImageGeneration)
        {
            throw new InvalidOperationException(
                "Isolated-host limitation: the production-seeded OpenAI image provider does not advertise image-generation support, so image_generation_create attachment cannot be asserted.");
        }

        return provider;
    }

    private static async Task<ProjectStructureReadResponse> ReadCanonicalGraphAsync(
        IServiceProvider services,
        Guid projectId)
    {
        await using var scope = services.CreateAsyncScope();
        var projectStructureService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        return await projectStructureService.GetStructureAsync(
            projectId,
            new ProjectStructureReadRequest(
                IncludeLinks: true,
                IncludeLayout: true,
                IncludeMetadata: true,
                IncludeNotes: true,
                IncludeAssets: true,
            Source: ProjectStructureReadSource.CanonicalCurrent));
    }

    private static async Task<ProjectStructureCanonicalGraphSnapshot> CaptureAcceptanceGraphAsync(
        IServiceProvider services,
        Guid projectId)
    {
        await using var scope = services.CreateAsyncScope();
        var capture = new ProjectStructureCanonicalGraphSnapshotCapture(
            scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>(),
            scope.ServiceProvider.GetRequiredService<ProjectsService>(),
            scope.ServiceProvider.GetRequiredService<IWorkspacePathAccessGuard>());
        return await capture.CaptureAsync(projectId);
    }

    private static string ComputeCanonicalGraphHash(ProjectStructureReadResponse graph)
    {
        var normalized = graph with
        {
            Nodes = graph.Nodes
                .OrderBy(node => node.Id, StringComparer.Ordinal)
                .ToArray(),
            Links = graph.Links
                .OrderBy(link => link.SourceId, StringComparer.Ordinal)
                .ThenBy(link => link.TargetId, StringComparer.Ordinal)
                .ThenBy(link => link.Kind)
                .ThenBy(link => link.IsUserAuthored)
                .ToArray(),
            Warnings = graph.Warnings
                .Order(StringComparer.Ordinal)
                .ToArray()
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(normalized, ApiJsonOptions);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static ProjectStructureCanonicalNodeSnapshot ToAcceptanceNodeSnapshot(
        ProjectStructureNodeSummary node)
    {
        return new ProjectStructureCanonicalNodeSnapshot(
            node.Id,
            node.ParentId,
            node.ObjectType,
            node.ObjectSubtype,
            node.Title,
            node.Subtitle,
            node.Status,
            node.Notes,
            node.MetadataJson,
            node.ArtifactKind,
            node.ArtifactId,
            node.ProgressMode,
            node.ProgressPercent,
            node.Priority,
            node.EffectivePriority,
            node.StartUtc,
            node.EndUtc,
            node.ProjectRole,
            node.RelatedProjectId,
            node.ParentProjectCount,
            node.X,
            node.Y,
            node.DurationSeconds);
    }

    private static async Task<CopyFixture> CreateCopyFixtureAsync(
        IServiceProvider services,
        Guid projectId)
    {
        await using var scope = services.CreateAsyncScope();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectRootId = $"project:{projectId:D}";
        var destination = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Real MAF copy destination",
                "Explicit destination parent",
                "The copied root must be attached only here.",
                projectRootId,
                ObjectSubtype: "planning"));
        var external = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Real MAF copy boundary",
                "Outside the selected subtree",
                "The source boundary link must be reported but not copied.",
                projectRootId));
        var sourceRoot = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Real MAF copy source",
                "Selected source root",
                "The complete editable subtree is copied exactly once.",
                projectRootId,
                ObjectSubtype: "architecture"));
        var sourceAsset = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Real MAF copy asset",
                "Managed Markdown child",
                "The managed bytes and hash must remain exact.",
                sourceRoot.Id,
                ObjectSubtype: "markdown",
                Media: new ProjectObjectMediaPayload(
                    "real-maf-copy-proof.md",
                    "text/markdown",
                    Convert.ToBase64String(CopyAssetBytes))));
        await workbench.LinkObjectsAsync(
            projectId,
            sourceRoot.Id,
            sourceAsset.Id,
            ProjectObjectLinkKind.Uses);
        await workbench.LinkObjectsAsync(
            projectId,
            sourceRoot.Id,
            external.Id,
            ProjectObjectLinkKind.DependsOn);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedBoundaryLink = await dbContext.Set<ProjectObjectLinkRecord>()
            .AsNoTracking()
            .SingleAsync(link =>
                link.ProjectId == projectId &&
                !link.IsSystemManaged &&
                link.SourceNodeKey == sourceRoot.Id &&
                link.TargetNodeKey == external.Id &&
                link.LinkKind == ProjectObjectLinkKind.DependsOn);
        var assembly = await scope.ServiceProvider
            .GetRequiredService<ProjectStructureAssemblyService>()
            .LoadAsync(dbContext, projectId);
        var expectedCopiedRootPosition = ProjectStructureAutomaticPlacementPolicy.Resolve(
            assembly.Nodes,
            new ProjectStructureAutomaticPlacementRequest(
                destination.Id,
                sourceRoot.ObjectType,
                sourceRoot.Title,
                sourceRoot.Subtitle,
                sourceRoot.Notes,
                (sourceRoot.X, sourceRoot.Y)));

        return new CopyFixture(
            destination,
            sourceRoot,
            sourceAsset,
            expectedCopiedRootPosition,
            [
                new ProjectStructureCopyOmittedLink(
                    persistedBoundaryLink.Id,
                    persistedBoundaryLink.SourceNodeKey,
                    persistedBoundaryLink.TargetNodeKey,
                    persistedBoundaryLink.LinkKind)
            ]);
    }

    private static async Task<ProjectSummary> CreateProjectAsync(HttpClient client)
    {
        return await PostAndReadAsync<ProjectSummary>(
            client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "Real MAF prompt harness",
                "Isolated canonical graph for deterministic MAF tool execution.",
                "Prove real MAF composition and Project Structure persistence.",
                "Validation",
                ProjectStatus.Active));
    }

    private static async Task<ProjectStructureNodeSummary> CreateParentNodeAsync(
        HttpClient client,
        Guid projectId)
    {
        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            client,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                "Create the deterministic MAF harness parent",
                5));
        try
        {
            return await PostAndReadAsync<ProjectStructureNodeSummary>(
                client,
                $"/api/project-structure/projects/{projectId:D}/nodes",
                new ProjectStructureNodeCreateInput(
                    ProjectObjectType.ProjectBlock,
                    "Exact parent",
                    "Parent for the deterministic real-MAF child",
                    string.Empty,
                    $"project:{projectId:D}",
                    ObjectSubtype: "architecture",
                    LeaseToken: lease.LeaseToken));
        }
        finally
        {
            await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
                client,
                "/api/project-structure/leases/release",
                new ProjectStructureLeaseReleaseRequest(
                    ProjectStructureLeaseScopeKind.Project,
                    projectId.ToString("D"),
                    lease.LeaseToken));
        }
    }

    private static async Task<ProjectStructureNodeSummary> CreateBaselineAssetAsync(
        HttpClient client,
        Guid projectId,
        string parentNodeId)
    {
        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            client,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                "Create the acceptance baseline asset",
                5));
        try
        {
            return await PostAndReadAsync<ProjectStructureNodeSummary>(
                client,
                $"/api/project-structure/projects/{projectId:D}/assets",
                new ProjectStructureAssetCreateInput(
                    ProjectObjectType.File,
                    "Acceptance baseline asset",
                    "Canonical bytes for exact-delta verification",
                    "The acceptance harness hashes this managed file before and after the agent run.",
                    new ProjectObjectMediaPayload(
                        "acceptance-baseline.md",
                        "text/markdown",
                        Convert.ToBase64String(BaselineAssetBytes)),
                    ParentNodeKey: parentNodeId,
                    ObjectSubtype: "markdown",
                    LeaseToken: lease.LeaseToken));
        }
        finally
        {
            await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
                client,
                "/api/project-structure/leases/release",
                new ProjectStructureLeaseReleaseRequest(
                    ProjectStructureLeaseScopeKind.Project,
                    projectId.ToString("D"),
                    lease.LeaseToken));
        }
    }

    private static async Task<HierarchyFixture> CreateHierarchyFixtureAsync(
        IServiceProvider services,
        Guid projectId)
    {
        await using var scope = services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var relatedProjectId = Guid.NewGuid();
        var unrelatedParentId = Guid.NewGuid();
        var unrelatedChildId = Guid.NewGuid();

        await RequireProjectCreatedAsync(
            projectsService.CreateSubprojectAsync(
                projectId,
                relatedProjectId,
                CreateProjectEditor("Acceptance related subproject")),
            "related subproject");
        await RequireProjectCreatedAsync(
            projectsService.CreateAsync(
                unrelatedParentId,
                CreateProjectEditor("Acceptance unrelated parent")),
            "unrelated parent");
        await RequireProjectCreatedAsync(
            projectsService.CreateSubprojectAsync(
                unrelatedParentId,
                unrelatedChildId,
                CreateProjectEditor("Acceptance unrelated child")),
            "unrelated child");

        return new HierarchyFixture(
            new ProjectStructureCanonicalHierarchyEdgeSnapshot(projectId, relatedProjectId),
            new ProjectStructureCanonicalHierarchyEdgeSnapshot(unrelatedParentId, unrelatedChildId));
    }

    private static ProjectEditorModel CreateProjectEditor(string name)
    {
        return new ProjectEditorModel
        {
            Name = name,
            Description = "Project Structure acceptance-harness hierarchy fixture.",
            Objective = "Prove exact hierarchy-edge verification.",
            CurrentPhase = "Validation",
            Status = ProjectStatus.Active
        };
    }

    private static async Task RequireProjectCreatedAsync(
        Task<Result<Guid>> operation,
        string description)
    {
        var result = await operation;
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Could not create the {description}: " +
                string.Join("; ", result.Errors.Select(error => error.Message)));
        }
    }

    private static async Task<T> PostAndReadAsync<T>(
        HttpClient client,
        string path,
        object request)
    {
        using var response = await client.PostAsJsonAsync(path, request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {body}");
        }

        return JsonSerializer.Deserialize<T>(body, ApiJsonOptions)
            ?? throw new InvalidOperationException($"No {typeof(T).Name} payload was returned.");
    }

    private static JsonSerializerOptions CreateApiJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record HierarchyFixture(
        ProjectStructureCanonicalHierarchyEdgeSnapshot RelatedEdge,
        ProjectStructureCanonicalHierarchyEdgeSnapshot UnrelatedEdge);

    private sealed record CopyFixture(
        ProjectStructureNode Destination,
        ProjectStructureNode SourceRoot,
        ProjectStructureNode SourceAsset,
        (double X, double Y) ExpectedCopiedRootPosition,
        IReadOnlyList<ProjectStructureCopyOmittedLink> ExpectedOmittedBoundaryLinks);
}
