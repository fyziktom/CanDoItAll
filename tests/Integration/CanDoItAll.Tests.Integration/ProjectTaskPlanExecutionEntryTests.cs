using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public enum UnsupportedPlanningRequest { Background, AutomaticApproval, Attachments, UnknownOwner }

public sealed class ProjectTaskPlanExecutionEntryTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registered_execution_admits_persisted_policy_and_waits_for_original_review(bool automaticAgent) {
        await using var fixture = await EntryFixture.CreateAsync();
        if (automaticAgent) {
            await fixture.Files.Store.UpdateCatalogAsync(current => current with {
                Agents = current.Agents.Select(agent => agent.Id == fixture.Files.Agent.Id ? agent with {
                    Permissions = agent.Permissions with { AutoApproveExternalCallsByDefault = true }
                } : agent).ToArray()
            });
        }
        var result = await fixture.Workspace.ExecuteRunAsync(fixture.Request);
        Assert.True(result.State == ExecutionState.WaitingOnTool, result.ResponseText);
        var detail = await fixture.Workspace.GetExecutionRunDetailAsync(result.ExecutionRunId);
        var journal = detail.Run.ToolAdmission!;
        Assert.Equal(AgentToolJournalRecord.PlanningSchemaVersion, journal.SchemaVersion);
        Assert.Equal(AgentPlanningGenerationState.AwaitingApproval, journal.PlanningDecision!.Current.State);
        Assert.Equal(fixture.Policy.Expectation.Namespace.Value, journal.PlanningDecision.Provenance.WorkspaceNamespaceId);
        Assert.Equal(fixture.Policy.Expectation.Head!.Revision, journal.PlanningDecision.Provenance.WorkspaceHeadRevision);
        Assert.Null(journal.PlanningDecision.Work.Open);
        Assert.Null(detail.Run.Outcome);
        Assert.Equal(journal.PlanningDecision.Current.Steps.Single().Publications.Single().Proposal,
            Assert.Single(detail.Run.PendingApprovals).ToolAdmission);
        Assert.False(string.IsNullOrWhiteSpace(AgentPlanningRunMetadata.Read(detail.Run).Value));
        Assert.Equal(11, Assert.Single(detail.Metrics).InputTokens);
        Assert.Equal(7, detail.Metrics.Single().OutputTokens);
        var usage = Assert.Single(detail.UsageObservations);
        Assert.Equal(11, usage.InputTokens);
        Assert.Equal(7, usage.OutputTokens);
        Assert.NotNull(usage.HistoryEvidence);
        Assert.Single(fixture.Wire.Requests);
        Assert.Single(fixture.Wire.Starts);
        Assert.Equal(result.ExecutionRunId.ToString("N"), fixture.Wire.Starts.Single().ContentOwner!.Owner.Value);
        using var request = JsonDocument.Parse(fixture.Wire.Requests.Single());
        Assert.Equal(2, request.RootElement.GetProperty("messages").GetArrayLength());
        Assert.False(request.RootElement.TryGetProperty("tools", out var tools) && tools.GetArrayLength() != 0);
        await fixture.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData("{\"unexpected\":true}")]
    [InlineData("")]
    public async Task Refused_provider_output_is_not_overwritten_as_success_and_preserves_usage(string responseText) {
        await using var fixture = await EntryFixture.CreateAsync();
        fixture.Wire.ResponseText = responseText;
        var result = await fixture.Workspace.ExecuteRunAsync(fixture.Request);
        Assert.Equal(ExecutionState.Failed, result.State);
        var detail = await fixture.Workspace.GetExecutionRunDetailAsync(result.ExecutionRunId);
        Assert.Equal(RunOutcome.Failed, detail.Run.Outcome);
        Assert.Equal(AgentPlanningGenerationState.Failed, detail.Run.ToolAdmission!.PlanningDecision!.Current.State);
        Assert.Null(detail.Run.ToolAdmission.PlanningDecision.Work.Open);
        Assert.Empty(detail.Run.PendingApprovals);
        Assert.Single(detail.Run.ToolAdmission.Batches);
        Assert.Equal(RunOutcome.Failed, Assert.Single(detail.Metrics).Outcome);
        Assert.Equal(11, Assert.Single(detail.UsageObservations).InputTokens);
        Assert.Equal(7, detail.UsageObservations.Single().OutputTokens);
        Assert.Contains(nameof(AgentPlanningAdmissionRefusal.InvalidProviderOutput), detail.Run.ResultSummary, StringComparison.Ordinal);
        Assert.Single(fixture.Wire.Requests);
        Assert.Single(fixture.Wire.Starts);
        await fixture.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Planning_uses_its_original_scope_and_own_material_basis(bool mismatched) {
        await using var fixture = await EntryFixture.CreateAsync();
        var originalScope = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Request.Context!.MetadataJson)!.WorkspaceScope;
        var request = fixture.Request with {
            TransientContext = new("obsolete-view-context", mismatched ? WorkspaceScopeDescriptor.Sandbox : originalScope)
        };
        var before = await fixture.Files.Store.GetChatSessionAsync(request.ChatSessionId!.Value);
        if (mismatched) {
            var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Workspace.ExecuteRunAsync(request));
            Assert.Equal("planning.unsupported", failure.Code);
            Assert.Empty(fixture.Wire.Requests);
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(await fixture.Files.Store.GetChatSessionAsync(request.ChatSessionId.Value)));
        } else {
            var result = await fixture.Workspace.ExecuteRunAsync(request);
            Assert.True(result.State == ExecutionState.WaitingOnTool, result.ResponseText);
            var detail = await fixture.Workspace.GetExecutionRunDetailAsync(result.ExecutionRunId);
            Assert.Equal(originalScope, AgentPlanningRunMetadata.ReadScope(detail.Run));
            Assert.DoesNotContain("obsolete-view-context", Assert.Single(fixture.Wire.Requests), StringComparison.Ordinal);
            Assert.Null(detail.Run.ToolAdmission!.RuntimeContext);
        }
        await fixture.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Cancellation_during_transport_remains_cancelled_after_response_finalization() {
        await using var fixture = await EntryFixture.CreateAsync();
        fixture.Wire.OnRequest = async token => {
            var id = Guid.Parse(fixture.Wire.Starts.Single().ContentOwner!.Owner.Value);
            await fixture.Files.Store.UpdateExecutionRunDetailAsync(id, current => current with { Run = current.Run with {
                Revision = current.Run.Revision + 1, State = ExecutionState.Completed, Outcome = RunOutcome.Cancelled,
                ToolAdmission = AgentToolJournalTransitions.RejectUndispatchedOnCancellation(current.Run.ToolAdmission!)
            } }, token);
        };
        var result = await fixture.Workspace.ExecuteRunAsync(fixture.Request);
        var detail = await fixture.Workspace.GetExecutionRunDetailAsync(result.ExecutionRunId);
        Assert.Equal(RunOutcome.Cancelled, detail.Run.Outcome);
        Assert.Equal(AgentPlanningGenerationState.Cancelled, detail.Run.ToolAdmission!.PlanningDecision!.Current.State);
        Assert.Null(detail.Run.ToolAdmission.PlanningDecision.Work.Open);
        Assert.Empty(detail.Run.PendingApprovals);
        Assert.Empty(detail.Run.ToolAdmission.Batches.Single().Proposals);
        Assert.Equal(RunOutcome.Cancelled, Assert.Single(detail.Metrics).Outcome);
        Assert.Equal(11, Assert.Single(detail.UsageObservations).InputTokens);
        Assert.Equal(7, detail.UsageObservations.Single().OutputTokens);
        await fixture.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(UnsupportedPlanningRequest.Background)]
    [InlineData(UnsupportedPlanningRequest.AutomaticApproval)]
    [InlineData(UnsupportedPlanningRequest.Attachments)]
    [InlineData(UnsupportedPlanningRequest.UnknownOwner)]
    public async Task Unsupported_planning_requests_refuse_before_run_admission(UnsupportedPlanningRequest unsupported) {
        await using var fixture = await EntryFixture.CreateAsync();
        var request = unsupported switch {
            UnsupportedPlanningRequest.Background => fixture.Request with { ChatSessionId = null },
            UnsupportedPlanningRequest.AutomaticApproval => fixture.Request with { AutoApprovePendingToolCalls = true },
            UnsupportedPlanningRequest.Attachments => fixture.Request with { InputAttachmentPaths = ["unread-planning-attachment"] },
            UnsupportedPlanningRequest.UnknownOwner => fixture.Request with { PlanningOwner = (AgentPlanningOwner)int.MaxValue },
            _ => throw new ArgumentOutOfRangeException(nameof(unsupported))
        };
        var before = await fixture.Files.Store.GetChatSessionAsync(fixture.Request.ChatSessionId!.Value);
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Workspace.ExecuteRunAsync(request));
        Assert.Equal("planning.unsupported", failure.Code);
        Assert.Empty(fixture.Wire.Requests);
        var after = await fixture.Files.Store.GetChatSessionAsync(fixture.Request.ChatSessionId.Value);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        await fixture.Files.Sql.VerifyAsync(0, 0);
    }

    internal sealed class EntryFixture(ProjectTaskPlanAgentFixture files, MafPlanningProviderFixture.Wire wire,
        HttpClient client, ExecutionRunRequest request, WorkspaceSettingsSnapshotV2 policy) : IAsyncDisposable {
        internal ProjectTaskPlanAgentFixture Files => files;
        internal MafPlanningProviderFixture.Wire Wire => wire;
        internal ExecutionRunRequest Request => request;
        internal WorkspaceSettingsSnapshotV2 Policy => policy;
        internal IAgentFrameworkWorkspaceService Workspace => files.Sql.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();

        internal static async Task<EntryFixture> CreateAsync(DecisionDataPolicy? requestedPolicy = null) {
            var wire = new MafPlanningProviderFixture.Wire { ResponseText = ProjectTaskPlanAgentFixture.Draft };
            var client = new HttpClient(wire);
            wire.Client = client;
            ProjectTaskPlanAgentFixture? files = null;
            try {
                files = await ProjectTaskPlanAgentFixture.CreateAsync(projectContext: true, configureServices: services => {
                    services.RemoveAll<IMafProviderAgentFactory>();
                    services.AddSingleton<IMafProviderAgentFactory>(new MafProviderAgentFactory(wire,
                        NoOpMafProviderStreamingDispatchGate.Instance, wire, httpClientSelector: wire));
                });
                var secret = await files.Sql.Services.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
                    Name = "Planning transport fixture credential", Kind = SecretKind.ApiKey,
                    SecretValue = "fixture-placeholder", Scope = "workspace"
                });
                Assert.True(secret.IsSuccess);
                var providers = files.Sql.Services.GetRequiredService<IProviderProfileRegistry>();
                var editor = await providers.GetProviderEditorAsync(files.Provider.Id);
                editor.Name = "Planning transport fixture";
                editor.Kind = ProviderKind.OpenAi;
                editor.Transport = ProviderTransportKind.ChatCompletions;
                editor.BaseUrl = "https://provider.example.test/v1";
                editor.DefaultModel = files.Provider.DefaultModel;
                editor.ApiKeyEnvironmentVariable = $"secret:{secret.Value:D}";
                editor.SupportsStreaming = false;
                Assert.Equal(files.Provider.Id, await providers.SaveProviderAsync(editor));
                Assert.Equal(ProviderTransportKind.ChatCompletions,
                    (await files.Sql.Services.GetRequiredService<IProviderRuntimeProfileSource>().GetProviderAsync(files.Provider.Id))!.Transport);
                var settings = files.Sql.Services.GetRequiredService<WorkspaceSettingsWriter>();
                var current = await settings.ReadV2Async(default);
                var policy = Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Accepted>(await settings.ReplaceV2Async(new(current.Expectation,
                    current.Values, requestedPolicy ?? DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10))), default)).Snapshot;
                var now = DateTimeOffset.UtcNow;
                var chat = await files.Store.CreateChatSessionAsync(new(Guid.NewGuid(), files.Agent.Id, "Product planning", now, now, []));
                var request = new ExecutionRunRequest(files.Agent.Id, "Plan the next reviewed tasks.", AgentExecutionOperationId.New(), chat.Id,
                    ExecutionInvocationContext.Empty with { SourceKind = files.Original.SourceKind, SourceId = string.Empty,
                        MetadataJson = files.Original.MetadataJson }) { PlanningOwner = AgentPlanningOwner.ProjectTaskPlan };
                return new(files, wire, client, request, policy);
            } catch {
                if (files is not null) {
                    await files.DisposeAsync();
                }
                client.Dispose();
                wire.Dispose();
                throw;
            }
        }

        public async ValueTask DisposeAsync() {
            try {
                await files.DisposeAsync();
            } finally {
                client.Dispose();
                wire.Dispose();
            }
        }
    }
}
