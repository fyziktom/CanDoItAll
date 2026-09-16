using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class WorkflowCuratorAgentRuntimeToolProviderTests {
    [Theory]
    [InlineData(CuratorOwnerAcknowledgementFault.BeforeOwner)]
    [InlineData(CuratorOwnerAcknowledgementFault.BeforeReceipt)]
    [InlineData(CuratorOwnerAcknowledgementFault.AfterReceipt)]
    public async Task Workflow_acknowledgement_survives_failed_projection_without_inventing_a_lost_receipt(
        CuratorOwnerAcknowledgementFault fault) {
        var catalog = new InMemoryWorkflowCatalogService(new InMemoryWorkflowCatalogStore(), new WorkflowDefinitionValidator());
        var service = DispatchProxy.Create<IWorkflowCatalogService, WorkflowAcknowledgementProxy>();
        var owner = (WorkflowAcknowledgementProxy)(object)service;
        owner.Inner = catalog;
        owner.Fault = fault;
        var harness = CreateHarness(owner: service);
        var tool = Assert.IsAssignableFrom<AIFunction>((await harness.Provider.CreateToolsAsync(harness.Context, default))
            .Single(item => item.Name == WorkflowCuratorToolPolicy.WorkflowCuratorDraftCreate));
        using var capture = AgentToolInvocationEffectScope.Begin();
        await Assert.ThrowsAsync<IOException>(() => tool.InvokeAsync(new AIFunctionArguments {
            ["request"] = new WorkflowCuratorDraftCreateInput("Acknowledged workflow", "Retained exact version.")
        }).AsTask());
        Assert.Equal(1, owner.Calls);
        if (fault == CuratorOwnerAcknowledgementFault.BeforeOwner) {
            Assert.Null(owner.Saved);
            Assert.Empty(await catalog.ListDefinitionsAsync());
        } else {
            var saved = Assert.IsType<WorkflowDefinition>(owner.Saved);
            var retained = await catalog.GetDefinitionAsync(saved.Id, saved.VersionId);
            Assert.NotNull(retained);
            Assert.Equal(saved.Id, retained.Definition.Id);
            Assert.Equal(saved.VersionId, retained.Definition.VersionId);
            Assert.Single(await catalog.ListDefinitionsAsync());
        }
        Assert.Equal(fault == CuratorOwnerAcknowledgementFault.AfterReceipt
            ? new AgentToolCommittedEffect("workflow-definition", owner.Saved!.Id.Value.ToString("D"))
            : null, capture.CommittedEffect);
    }

    private static void AssertOwnerAcknowledgement<T>(string toolName, T result, AgentToolCommittedEffect? actual) {
        AgentToolCommittedEffect? expected = toolName is WorkflowCuratorToolPolicy.WorkflowCuratorDraftCreate or
            WorkflowCuratorToolPolicy.WorkflowCuratorDraftUpdate or WorkflowCuratorToolPolicy.WorkflowCuratorNodeUpdate or
            WorkflowCuratorToolPolicy.WorkflowCuratorLifecycleChange
            ? new("workflow-definition", Assert.IsType<WorkflowCuratorDefinitionEditorResult>(result).Definition.Id.Value.ToString("D"))
            : null;
        Assert.Equal(expected, actual);
    }

    private class WorkflowAcknowledgementProxy : DispatchProxy {
        public IWorkflowCatalogService Inner { get; set; } = null!;
        public CuratorOwnerAcknowledgementFault Fault { get; set; }
        public WorkflowDefinition? Saved { get; private set; }
        public int Calls { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch {
            nameof(IWorkflowCatalogService.SaveDefinitionAsync) => SaveAsync((WorkflowDefinitionSaveRequest)args![0]!, (CancellationToken)args[1]!),
            nameof(IWorkflowCatalogService.GetDefinitionAsync) => throw new IOException("Read after the returned Workflow receipt failed."),
            _ => throw new NotSupportedException(targetMethod?.Name)
        };

        private async Task<WorkflowDefinition> SaveAsync(WorkflowDefinitionSaveRequest request, CancellationToken token) {
            Calls++;
            if (Fault == CuratorOwnerAcknowledgementFault.BeforeOwner) {
                throw new IOException("Workflow save was not started.");
            }
            Saved = await Inner.SaveDefinitionAsync(request, token);
            if (Fault == CuratorOwnerAcknowledgementFault.BeforeReceipt) {
                throw new IOException("The Workflow acknowledgement was lost.");
            }
            return Saved;
        }
    }
}
