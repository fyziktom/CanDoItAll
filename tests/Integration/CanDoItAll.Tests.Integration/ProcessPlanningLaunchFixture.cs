using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using EntryFixture = CanDoItAll.Tests.Integration.ProjectTaskPlanExecutionEntryTests.EntryFixture;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace CanDoItAll.Tests.Integration;

internal sealed class ProcessPlanningLaunchFixture(CanDoItAllTestEnvironment environment, EntryFixture entry,
    ApiTestHost host, ProcessLaunchApiRequest request, ProcessPlanningLaunchFixture.SelectedExecutor resolver,
    ConcurrentQueue<Exception> startupFailures) : IAsyncDisposable {
    private const string DefinitionKey = "dotnet-solution-setup";
    internal EntryFixture Entry => entry;
    internal ApiTestHost Host => host;
    internal ProcessLaunchApiRequest Request => request;
    internal SelectedExecutor Resolver => resolver;
    internal string StartupFailure => string.Join("; ", startupFailures.Select(failure => failure.GetBaseException().Message));
    internal JsonSerializerOptions Json => host.App.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

    internal static async Task<ProcessPlanningLaunchFixture> CreateAsync(ReadBasisMode mode = ReadBasisMode.StepSnapshot,
        int activities = 1, bool allowTaskOperation = true, OnBasisChange response = OnBasisChange.PauseForReview) {
        var environment = CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        EntryFixture? entry = null;
        ApiTestHost? host = null;
        try {
            var profile = environment.CreatePostgreSqlProfile("primary");
            entry = await EntryFixture.CreateAsync(new(mode, PublicationFreshness.RequireCurrent, response,
                new(0, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10))), environment, profile);
            var providers = entry.Files.Sql.Services.GetRequiredService<IProviderProfileRegistry>();
            var provider = await providers.GetProviderEditorAsync(entry.Files.Provider.Id);
            provider.Id = null;
            provider.ExpectedConcurrencyToken = null;
            provider.Name = "Public planning transport fixture";
            var providerId = await providers.SaveProviderAsync(provider);
            await entry.Files.Store.UpdateCatalogAsync(current => current with {
                Agents = current.Agents.Select(agent => agent.Id == entry.Files.Agent.Id ? agent with { ProviderProfileId = providerId } : agent).ToArray()
            });
            var selected = new SelectedExecutor(entry.Files.Agent.Id);
            ConcurrentQueue<Exception> startupFailures = new();
            host = await ApiTestHost.CreateAsync(false, services => {
                services.AddLogging(logging => logging.AddFilter(DbLoggerCategory.Database.Command.Name, LogLevel.Warning));
                services.Replace(ServiceDescriptor.Singleton<IProcessLaunchExecutorResolver>(selected));
                services.RemoveAll<IMafProviderAgentFactory>();
                services.AddSingleton(entry.Files.Sql.Services.GetRequiredService<IMafProviderAgentFactory>());
                services.Replace(ServiceDescriptor.Scoped<IAgentInheritedPlanningWorkspaceService>(provider =>
                    new ObservedWorkspace(provider.GetRequiredService<CurrentProfileAgentFrameworkWorkspaceService>(), startupFailures)));
            }, sharedTestEnvironment: environment, sharedActiveProfile: profile);
            await using var scope = host.App.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            Assert.Equal(ProviderTransportKind.ChatCompletions,
                (await services.GetRequiredService<IProviderRuntimeProfileSource>().GetProviderAsync(providerId))!.Transport);
            var authoring = services.GetRequiredService<ProcessAuthoringWorkspace>();
            var baseline = await authoring.ReadAsync(ProcessWorkspaceShellScope.ForProject(entry.Files.Sql.Project.ProjectId), new(DefinitionKey), default);
            var content = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(baseline.Content));
            content.Definition.DisplayName = "Governed task planning";
            content.Definition.LaunchDriverActivations.Clear();
            content.Definition.Steps = Enumerable.Range(0, activities).Select(index => new ProcessTemplateDefinitionStepDocument {
                Key = $"plan-{index}", Title = "Plan reviewed Project tasks", StepKind = "Work",
                Notes = "Plan the next reviewed tasks.", OperationTargetScope = ProcessOperationContractNames.ManagedProcessArtifactsOnly,
                AllowedOperations = allowTaskOperation ? [ProcessOperationContractNames.ExecuteExternalAction] : [],
                RoleAssignments = [new() { RoleKey = content.Definition.RoleUsages[0].Key }]
            }).ToList();
            content = content with { Guidance = new Dictionary<string, IReadOnlyList<ProcessTemplateExecutionGuidanceDocument>>() };
            content = await services.GetRequiredService<ProcessExecutableDefinitionResolver>().PinPublicationAsync(baseline, content, default);
            var published = await authoring.CommitAsync(baseline, Guid.NewGuid(), ProcessAuthoringCodec.Hash("Governed task planning"), content,
                ProcessAuthoringLifecycle.Published, true, null, default);
            Assert.Equal(ProcessAuthoringOutcome.Accepted, published.Outcome);
            var request = new ProcessLaunchApiRequest(DefinitionKey: DefinitionKey, ProjectId: entry.Files.Sql.Project.ProjectId,
                RunReadiness: false, CallerIntentId: Guid.NewGuid(), Decision: new(1, ProjectTaskPlanProcessContract.Execution));
            return new(environment, entry, host, request, selected, startupFailures);
        } catch {
            if (host is not null) {
                await host.DisposeAsync();
            }
            if (entry is not null) {
                await entry.DisposeAsync();
            }
            await environment.DisposeAsync();
            throw;
        }
    }

    internal async Task<JsonDocument> PostAsync(string route, ProcessLaunchApiRequest input) {
        using var response = await host.Client.PostAsJsonAsync(route, input);
        return await ReadAsync(response);
    }

    internal static async Task<JsonDocument> ReadAsync(HttpResponseMessage response) {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {body}");
        return JsonDocument.Parse(body);
    }

    internal async Task<ProcessPreparedLaunchSnapshot> SavedAsync(Guid? intent = null) {
        await using var scope = host.App.Services.CreateAsyncScope();
        return Assert.IsType<ProcessPreparedLaunchSnapshot>(await scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>()
            .FindByIntentAsync(new(intent ?? request.CallerIntentId!.Value)));
    }

    internal async Task<WorkspaceSettingsSnapshotV2> SetPolicyAsync(DecisionDataPolicy policy) {
        await using var scope = host.App.Services.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<WorkspaceSettingsWriter>();
        var current = await settings.ReadV2Async(default);
        return Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Accepted>(await settings.ReplaceV2Async(new(current.Expectation, current.Values, policy), default)).Snapshot;
    }

    internal async Task RestoreLegacySettingsWithoutPolicyAsync() {
        await using var scope = host.App.Services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkspaceSettingsDbContext>>().CreateDbContextAsync();
        var row = await context.Set<WorkspaceSettings>().SingleAsync(item => item.IsCurrent);
        row.DecisionDataPolicyJson = null;
        await context.SaveChangesAsync();
        Assert.Null((await scope.ServiceProvider.GetRequiredService<WorkspaceSettingsWriter>().ReadV2Async(default)).DecisionDataPolicy);
    }

    public async ValueTask DisposeAsync() {
        try {
            await host.DisposeAsync();
        } finally {
            try {
                await entry.DisposeAsync();
            } finally {
                await environment.DisposeAsync();
            }
        }
    }

    private sealed class ObservedWorkspace(IAgentInheritedPlanningWorkspaceService inner,
        ConcurrentQueue<Exception> failures) : IAgentInheritedPlanningWorkspaceService {
        public async Task<AgentInheritedPlanningWorkspaceResult> ExecuteInheritedPlanningAsync(AgentInheritedPlanningWorkspaceRequest request,
            CancellationToken cancellationToken = default) {
            try {
                return await inner.ExecuteInheritedPlanningAsync(request, cancellationToken);
            } catch (AgentInheritedPlanningPreparationException exception) {
                failures.Enqueue(exception);
                throw;
            }
        }

        public Task<AgentInheritedPlanningWorkspaceResult> ExecuteInheritedPlanningWithinOperationAsync(IAgentExecutionActivityOperationLease operation,
            AgentInheritedPlanningWorkspaceRequest request, CancellationToken cancellationToken = default)
            => inner.ExecuteInheritedPlanningWithinOperationAsync(operation, request, cancellationToken);

        public Task<T> ReviewInheritedPlanningAsync<T>(AgentInheritedPlanningReviewRequest request, AgentInheritedSavedEffectContinuation authority,
            AgentToolSemanticDigest ownerIntent, AgentExecutionOperationId activityOperationId,
            Func<AgentInheritedPlanningReviewResult, CancellationToken, Task<T>> settleOriginal, CancellationToken cancellationToken = default)
            => inner.ReviewInheritedPlanningAsync(request, authority, ownerIntent, activityOperationId, settleOriginal, cancellationToken);

        public Task<T> ReviewInheritedPlanningWithinOperationAsync<T>(IAgentExecutionActivityOperationLease operation,
            AgentInheritedPlanningReviewRequest request, AgentInheritedSavedEffectContinuation authority, AgentToolSemanticDigest ownerIntent,
            Func<AgentInheritedPlanningReviewResult, CancellationToken, Task<T>> settleOriginal, CancellationToken cancellationToken = default)
            => inner.ReviewInheritedPlanningWithinOperationAsync(operation, request, authority, ownerIntent, settleOriginal, cancellationToken);
    }

    internal sealed class SelectedExecutor(Guid agentId) : IProcessLaunchExecutorResolver {
        private readonly TaskCompletionSource bothEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int calls;
        internal bool RequireConcurrentEntry { get; set; }

        public async ValueTask<ProcessLaunchExecutorResolution> ResolveAsync(ProcessLaunchExecutorResolutionRequest request,
            CancellationToken cancellationToken = default) {
            if (RequireConcurrentEntry) {
                if (Interlocked.Increment(ref calls) == 2) {
                    bothEntered.TrySetResult();
                }
                await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return new(request.Plan.Steps.Where(step => step.IsExecutable).Select(step => new ProcessLaunchExecutorBinding(step.StepKey,
                request.Definition.Steps.Single(item => item.Key == step.StepKey).RoleAssignments[0].RoleKey,
                ProcessLaunchExecutorKinds.Agent, agentId.ToString("D"), "Selected planning Agent", "sha256:planning-agent", "Explicit fixture executor")).ToArray(), []);
        }
    }
}
