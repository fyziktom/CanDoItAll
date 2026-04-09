using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.Processes;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Mcp.Processes.Tests;

public sealed class ProcessesToolsTests
{
    [Fact]
    public async Task ProcessesDefinitionSaveAsync_returns_successful_structured_content()
    {
        var definitionId = Guid.NewGuid();
        var tools = new ProcessesTools(new StubCoordinator
        {
            OnSaveDefinition = (_, _) => Task.FromResult(definitionId)
        }, NullLogger<ProcessesTools>.Instance);

        var result = await tools.ProcessesDefinitionSaveAsync(new ProcessDefinitionEditorModel
        {
            Name = "Quality gate",
            ValueStatement = "Keep release governance explicit.",
            OwnerName = "Morgan",
            Roles = [new ProcessRoleEditorModel { DisplayName = "Release lead" }],
            Steps = [new ProcessStepEditorModel { Title = "Review release" }]
        });

        Assert.True(result.Ok);
        Assert.Equal(definitionId, result.Data);
    }

    [Fact]
    public async Task ProcessesRunDetailGetAsync_returns_successful_structured_content()
    {
        var runId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var tools = new ProcessesTools(new StubCoordinator
        {
            OnGetRunDetail = (_, _) => Task.FromResult(
                new ProcessRunDetailToolData(
                    new ProcessRunListItem(
                        runId,
                        definitionId,
                        Guid.NewGuid(),
                        null,
                        "Release validation",
                        ProcessRunStatus.Active,
                        ProcessOperatingMode.AssistedExecution,
                        1,
                        2,
                        0,
                        0,
                        120m,
                        40m,
                        DateTimeOffset.UtcNow),
                    [],
                    [],
                    [],
                    [],
                    [],
                    [],
                    []))
        }, NullLogger<ProcessesTools>.Instance);

        var result = await tools.ProcessesRunDetailGetAsync(runId);

        Assert.True(result.Ok);
        Assert.Equal(runId, result.Data!.Run.Id);
    }

    [Fact]
    public async Task ProcessesDefinitionPublishAsync_returns_structured_validation_failure()
    {
        var tools = new ProcessesTools(new StubCoordinator
        {
            OnPublishDefinition = (_, _) => throw new ToolInvocationException("processes.publish-governance-required", "Governance summary is required before publication.")
        }, NullLogger<ProcessesTools>.Instance);

        var result = await tools.ProcessesDefinitionPublishAsync(Guid.NewGuid());

        Assert.False(result.Ok);
        Assert.Equal("processes.publish-governance-required", result.Error!.Code);
        Assert.Equal("validation_error", result.Status);
    }

    private sealed class StubCoordinator : IProcessesCoordinator
    {
        public Func<Guid?, CancellationToken, Task<IReadOnlyList<ProcessDefinitionListItem>>>? OnListDefinitions { get; init; }

        public Func<Guid?, Guid?, CancellationToken, Task<ProcessDefinitionEditorModel>>? OnGetDefinitionEditor { get; init; }

        public Func<ProcessDefinitionEditorModel, CancellationToken, Task<Guid>>? OnSaveDefinition { get; init; }

        public Func<Guid, CancellationToken, Task>? OnPublishDefinition { get; init; }

        public Func<Guid, CancellationToken, Task>? OnDeleteDefinition { get; init; }

        public Func<Guid, CancellationToken, Task<ProcessImportExportEnvelope>>? OnExportDefinition { get; init; }

        public Func<ProcessImportExportEnvelope, CancellationToken, Task<Guid>>? OnImportDefinition { get; init; }

        public Func<Guid?, Guid?, CancellationToken, Task<IReadOnlyList<ProcessRunListItem>>>? OnListRuns { get; init; }

        public Func<Guid, CancellationToken, Task<ProcessRunDetailToolData>>? OnGetRunDetail { get; init; }

        public Func<Guid?, Guid?, CancellationToken, Task<ProcessAnalyticsSummary>>? OnGetAnalytics { get; init; }

        public Func<ProcessRunStartRequest, CancellationToken, Task<Guid>>? OnStartRun { get; init; }

        public Func<ProcessStepTransitionRequest, CancellationToken, Task>? OnTransitionStep { get; init; }

        public Func<ProcessAssignmentResolutionRequest, CancellationToken, Task>? OnResolveAssignment { get; init; }

        public Func<ProcessArtifactRecordRequest, CancellationToken, Task<Guid>>? OnRecordArtifact { get; init; }

        public Func<Guid, CancellationToken, Task<IReadOnlyList<ProjectPartyOption>>>? OnListPartyOptions { get; init; }

        public Func<CancellationToken, Task<IReadOnlyList<ProcessExecutorRegistryOption>>>? OnListExecutorOptions { get; init; }

        public Task<IReadOnlyList<ProcessDefinitionListItem>> ListDefinitionsAsync(Guid? projectId, CancellationToken cancellationToken = default)
        {
            return OnListDefinitions?.Invoke(projectId, cancellationToken) ?? Task.FromResult<IReadOnlyList<ProcessDefinitionListItem>>([]);
        }

        public Task<ProcessDefinitionEditorModel> GetDefinitionEditorAsync(Guid? definitionId, Guid? projectId, CancellationToken cancellationToken = default)
        {
            return OnGetDefinitionEditor?.Invoke(definitionId, projectId, cancellationToken) ?? Task.FromResult(new ProcessDefinitionEditorModel());
        }

        public Task<Guid> SaveDefinitionAsync(ProcessDefinitionEditorModel model, CancellationToken cancellationToken = default)
        {
            return OnSaveDefinition?.Invoke(model, cancellationToken) ?? Task.FromResult(Guid.NewGuid());
        }

        public async Task PublishDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default)
        {
            if (OnPublishDefinition is not null)
            {
                await OnPublishDefinition(definitionId, cancellationToken);
            }
        }

        public async Task DeleteDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default)
        {
            if (OnDeleteDefinition is not null)
            {
                await OnDeleteDefinition(definitionId, cancellationToken);
            }
        }

        public Task<ProcessImportExportEnvelope> ExportDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default)
        {
            return OnExportDefinition?.Invoke(definitionId, cancellationToken) ?? Task.FromResult(new ProcessImportExportEnvelope());
        }

        public Task<Guid> ImportDefinitionAsync(ProcessImportExportEnvelope envelope, CancellationToken cancellationToken = default)
        {
            return OnImportDefinition?.Invoke(envelope, cancellationToken) ?? Task.FromResult(Guid.NewGuid());
        }

        public Task<IReadOnlyList<ProcessRunListItem>> ListRunsAsync(Guid? definitionId, Guid? projectId, CancellationToken cancellationToken = default)
        {
            return OnListRuns?.Invoke(definitionId, projectId, cancellationToken) ?? Task.FromResult<IReadOnlyList<ProcessRunListItem>>([]);
        }

        public Task<ProcessRunDetailToolData> GetRunDetailAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            return OnGetRunDetail?.Invoke(runId, cancellationToken)
                ?? Task.FromResult(
                    new ProcessRunDetailToolData(
                        new ProcessRunListItem(
                            runId,
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            null,
                            "Run",
                            ProcessRunStatus.Active,
                            ProcessOperatingMode.AssistedExecution,
                            0,
                            0,
                            0,
                            0,
                            0m,
                            0m,
                            DateTimeOffset.UtcNow),
                        [],
                        [],
                        [],
                        [],
                        [],
                        [],
                        []));
        }

        public Task<ProcessAnalyticsSummary> GetAnalyticsAsync(Guid? definitionId, Guid? projectId, CancellationToken cancellationToken = default)
        {
            return OnGetAnalytics?.Invoke(definitionId, projectId, cancellationToken)
                ?? Task.FromResult(new ProcessAnalyticsSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0m, 0m));
        }

        public Task<Guid> StartRunAsync(ProcessRunStartRequest request, CancellationToken cancellationToken = default)
        {
            return OnStartRun?.Invoke(request, cancellationToken) ?? Task.FromResult(Guid.NewGuid());
        }

        public async Task TransitionStepAsync(ProcessStepTransitionRequest request, CancellationToken cancellationToken = default)
        {
            if (OnTransitionStep is not null)
            {
                await OnTransitionStep(request, cancellationToken);
            }
        }

        public async Task ResolveAssignmentAsync(ProcessAssignmentResolutionRequest request, CancellationToken cancellationToken = default)
        {
            if (OnResolveAssignment is not null)
            {
                await OnResolveAssignment(request, cancellationToken);
            }
        }

        public Task<Guid> RecordArtifactAsync(ProcessArtifactRecordRequest request, CancellationToken cancellationToken = default)
        {
            return OnRecordArtifact?.Invoke(request, cancellationToken) ?? Task.FromResult(Guid.NewGuid());
        }

        public Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            return OnListPartyOptions?.Invoke(projectId, cancellationToken) ?? Task.FromResult<IReadOnlyList<ProjectPartyOption>>([]);
        }

        public Task<IReadOnlyList<ProcessExecutorRegistryOption>> ListExecutorOptionsAsync(CancellationToken cancellationToken = default)
        {
            return OnListExecutorOptions?.Invoke(cancellationToken) ?? Task.FromResult<IReadOnlyList<ProcessExecutorRegistryOption>>([]);
        }
    }
}
