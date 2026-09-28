using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

/// <summary>
/// Opt-in live smoke: a real model drives the registered CRM planning tools through the production governed runtime.
/// Every proof is read from the persisted run (tool-admission journal, tool receipts and the context assembly manifest)
/// and from the CRM owner, never from the model's prose. The managed HR approval flow needs the chat context the Agents
/// page publishes and is proven through the shipped UI in <c>CrmHrLiveAgentToolUiSmokeTests</c>. Deterministic
/// fake-model coverage lives in <c>CrmPlanningRuntimeIntegrationTests</c> and <c>MafHrResultDisclosureIntegrationTests</c>.
/// </summary>
[Trait("Category", "HostPlatform")]
public sealed partial class CrmHrLiveAgentToolSmokeIntegrationTests
{
    private const string LiveValidationVariable = "CANDOITALL_RUN_LIVE_AGENT_VALIDATION";
    private const string LiveOpenAiSmokeVariable = "CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE";
    private const string PreferredPlannerTemplateKey = "delivery-manager";
    private const string FixtureActor = "live-agent-smoke-fixture";

    // Spend bound for one execution. A turn is cancelled as soon as one response more than its allowance is admitted,
    // and the allowance leaves room for that response and one request already in flight.
    private const int MaximumModelRequestsPerExecution = 8;
    private const int MaximumModelRequestsPerTurn = 3;
    private const int MinimumModelRequestsPerTurn = 2;
    private const int CancellationOvershootReserve = 2;
    private static readonly TimeSpan TurnTimeout = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromMilliseconds(400);

    [Fact]
    [Trait("Category", "LiveAgent")]
    public async Task Ordinary_planner_invokes_the_CRM_planning_search_tool_through_the_live_governed_runtime()
    {
        if (!IsLiveValidationEnabled())
        {
            return;
        }

        var evidence = new SmokeEvidence("ordinary-planner-crm-planning-read");
        await using var live = await LiveHost.CreateAsync("live-crm-planner");
        try
        {
            var services = live.Services;
            var workspace = live.Workspace;
            var suffix = Guid.NewGuid().ToString("N")[..10];
            var personName = $"Planning Smoke Person {suffix}";

            var projectId = await CreateProjectAsync(services, $"Live planning smoke {suffix}");
            var personId = await CreatePartyAsync(services, PartyType.Person, personName);
            var profileResult = await services.GetRequiredService<HrService>().SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
            {
                PartyId = personId,
                WorkforceKind = WorkforceKind.Employee,
                Status = "Active",
                JobTitle = "Synthetic planner",
                LastChangedBy = FixtureActor
            });
            Assert.True(profileResult.IsSuccess, "The synthetic workforce profile was not saved by the HR owner.");
            evidence.Targets["projectId"] = projectId;
            evidence.Targets["personPartyId"] = personId;
            evidence.Targets["personName"] = personName;

            var planner = await SelectOrdinaryPlannerAsync(workspace);
            Assert.NotEqual(HrAgentIdentity.AgentId, planner.Id);
            var planningKeys = new[] { CrmPlanningToolPolicy.SearchCapability, CrmPlanningToolPolicy.SummaryCapability };
            Assert.DoesNotContain(planner.Capabilities, item => planningKeys.Contains(item.CapabilityKey));
            Assert.DoesNotContain(MemorySourceScope.Crm, AgentMemoryAccessMetadata.Read(planner.ConfigurationJson).AllowedSourceScopes);
            var planningCapabilities = (await workspace.ListCapabilitiesAsync())
                .Where(item => item.Kind == CapabilityKind.Tool && planningKeys.Contains(item.Key))
                .ToArray();
            Assert.Equal(planningKeys.Length, planningCapabilities.Length);

            planner = await SavePlannerGrantsAsync(workspace, planner.Id, projectId, planningCapabilities, crmScope: true);
            Assert.Equal(
                planningKeys.Order(StringComparer.Ordinal),
                planner.Capabilities.Where(item => planningKeys.Contains(item.CapabilityKey))
                    .Select(item => item.CapabilityKey).Order(StringComparer.Ordinal));
            Assert.Contains(MemorySourceScope.Crm, AgentMemoryAccessMetadata.Read(planner.ConfigurationJson).AllowedSourceScopes);
            await evidence.DescribeProviderAsync(live, planner);

            var countsBefore = await ReadOwnerCountsAsync(services);
            evidence.CountsBefore = countsBefore;
            using var projectContext = await ActivateProjectStructureContextAsync(services, workspace, projectId);

            var read = await live.SendAsync(planner.Id,
                $"Call the {CrmPlanningToolPolicy.Search} tool exactly once with searchText \"{personName}\", recordKind Party and take 5. " +
                "Do not call any other tool. Reply with only the id of the party it returned.");
            var readTurn = await evidence.RecordTurnAsync(live, "planner-read", read);
            evidence.Observations["assistantAnswerContainsPartyId"] =
                read.AssistantContent.Contains(personId.ToString("D"), StringComparison.OrdinalIgnoreCase);

            // Positive proof: persisted journal, receipt and owner-backed payload for the resolved tool name.
            Assert.Equal(ExecutionState.Completed, readTurn.Detail.Run.State);
            Assert.Equal(AgentToolAdmissionSupport.Recoverable, readTurn.Detail.Run.ToolAdmission?.Support);
            var search = Assert.Single(readTurn.Proposals, item => item.Payload.ToolName == CrmPlanningToolPolicy.Search);
            Assert.Equal(AgentToolProposalState.Completed, search.State);
            Assert.Equal(AgentToolProposalEffect.Read, search.Payload.Effect);
            Assert.False(search.RequiresApproval);
            Assert.Null(search.ApprovalId);
            Assert.Contains(personName, search.Result!.PayloadJson, StringComparison.Ordinal);
            Assert.Contains(personId.ToString("D"), search.Result.PayloadJson, StringComparison.OrdinalIgnoreCase);
            Assert.All(readTurn.Proposals, item => Assert.NotEqual(AgentToolProposalEffect.Mutation, item.Payload.Effect));
            Assert.All(readTurn.Proposals.Where(item => item.Payload.ToolName == CrmPlanningToolPolicy.Summary), item =>
            {
                Assert.Equal(AgentToolProposalState.Completed, item.State);
                Assert.False(item.RequiresApproval);
            });
            Assert.Empty(readTurn.Detail.Run.PendingApprovals);
            Assert.Empty(readTurn.Detail.Approvals);
            var receipt = Assert.Single(readTurn.Detail.ToolReceipts, item => item.ToolName == CrmPlanningToolPolicy.Search);
            Assert.Equal(CrmPlanningToolPolicy.ProviderKey, receipt.RuntimeToolProviderKey);
            Assert.Equal(AgentToolInvocationOutcome.Succeeded, receipt.InvocationOutcome);
            Assert.Equal(ToolExecutionSideEffectMode.NoMutation, receipt.DeclaredSideEffectMode);
            Assert.Equal(countsBefore, await ReadOwnerCountsAsync(services));

            // Composition proof from the persisted context manifest of the real run: the planning provider attached
            // exactly the granted tools and the managed HR provider attached nothing to the ordinary planner.
            var granted = Assert.Single(readTurn.ToolProviders, item => item.ProviderKey == CrmPlanningToolPolicy.ProviderKey);
            Assert.True(granted.Included);
            Assert.Equal(planningKeys.Length, granted.ToolCount);
            Assert.DoesNotContain(readTurn.ToolProviders, item => item.ProviderKey == HrAgentRuntimeToolProvider.ProviderKey && item.Included);

            // Negative control: the same agent and project source without the CRM source-read scope.
            var revoked = await SavePlannerGrantsAsync(workspace, planner.Id, projectId, planningCapabilities, crmScope: false);
            Assert.DoesNotContain(MemorySourceScope.Crm, AgentMemoryAccessMetadata.Read(revoked.ConfigurationJson).AllowedSourceScopes);
            var control = await live.SendAsync(planner.Id, "Reply with the single word READY. Do not call any tool.");
            var controlTurn = await evidence.RecordTurnAsync(live, "planner-without-crm-source-scope", control);
            evidence.CountsAfter = await ReadOwnerCountsAsync(services);
            Assert.Equal(countsBefore, evidence.CountsAfter);
            Assert.NotEmpty(controlTurn.ToolProviders);
            Assert.DoesNotContain(controlTurn.ToolProviders, item => item.ProviderKey == CrmPlanningToolPolicy.ProviderKey && item.Included);
            Assert.DoesNotContain(controlTurn.ToolProviders, item => item.ProviderKey == HrAgentRuntimeToolProvider.ProviderKey && item.Included);
            Assert.DoesNotContain(controlTurn.Proposals, item =>
                item.Payload.ToolName is CrmPlanningToolPolicy.Search or CrmPlanningToolPolicy.Summary);
            Assert.DoesNotContain(controlTurn.Detail.ToolReceipts, item => item.RuntimeToolProviderKey == CrmPlanningToolPolicy.ProviderKey);
            evidence.Passed = true;
        }
        finally
        {
            await evidence.WriteAsync(live);
        }

        Assert.InRange(evidence.ModelRequests, 1, MaximumModelRequestsPerExecution);
    }

    // The runner reports a closed gate as a pass, so the evidence manifest of this lane is the only place that says
    // whether a model was actually reached; a run that returns here writes no manifest at all.
    private static bool IsLiveValidationEnabled()
        => IsEnabled(LiveValidationVariable) && IsEnabled(LiveOpenAiSmokeVariable);

    private static bool IsEnabled(string environmentVariable)
        => string.Equals(Environment.GetEnvironmentVariable(environmentVariable), "true", StringComparison.OrdinalIgnoreCase);

    private static async Task<Guid> CreateProjectAsync(IServiceProvider services, string name)
    {
        var result = await services.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = "Synthetic project for the opt-in live agent tool smoke.",
            Objective = "Prove governed CRM planning reads.",
            CurrentPhase = "Validation"
        });
        Assert.True(result.IsSuccess, "The synthetic project was not saved by the Projects owner.");
        return result.Value;
    }

    private static async Task<Guid> CreatePartyAsync(IServiceProvider services, PartyType partyType, string displayName)
    {
        var result = await services.GetRequiredService<ICrmPartyCommandService>().CreatePartyAsync(
            new(partyType, displayName, PartyLifecycleStatus.Active), FixtureActor);
        Assert.True(result.IsSuccess, "The synthetic CRM party was not saved by the CRM owner.");
        return result.Value!.PartyId;
    }

    private static async Task<AgentDefinition> SelectOrdinaryPlannerAsync(IAgentFrameworkWorkspaceService workspace)
    {
        var candidates = (await workspace.ListAgentsAsync(includeTemplates: false))
            .Where(agent => !agent.IsTemplate && agent.Status == AgentLifecycleStatus.Active && agent.Permissions.CanUseTools &&
                agent.ProviderProfileId.HasValue && !ManagedAdministrativeAgentIdentityCatalog.AgentIds.Contains(agent.Id) &&
                AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson).CanRead)
            .OrderBy(agent => agent.TemplateKey == PreferredPlannerTemplateKey ? 0 : 1)
            .ThenBy(agent => agent.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(candidates);
        return candidates[0];
    }

    // The same owner editor round trip the Agents settings UI performs: capability selection, the CRM source-read
    // scope on the memory settings panel and project access.
    private static async Task<AgentDefinition> SavePlannerGrantsAsync(IAgentFrameworkWorkspaceService workspace, Guid agentId,
        Guid projectId, IReadOnlyList<CapabilityCatalogItem> planningCapabilities, bool crmScope)
    {
        var editor = await workspace.GetAgentEditorAsync(agentId);
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Concat(planningCapabilities.Select(item => item.Id)).Distinct().ToList();
        editor.MemoryAccess.AllowedSourceScopes = crmScope
            ? editor.MemoryAccess.AllowedSourceScopes.Append(MemorySourceScope.Crm).Distinct().ToArray()
            : editor.MemoryAccess.AllowedSourceScopes.Where(scope => scope != MemorySourceScope.Crm).ToArray();
        if (!editor.ProjectStructureAccess.AllowAllProjects && !editor.ProjectStructureAccess.AllowedProjectIds.Contains(projectId))
        {
            editor.ProjectStructureAccess.CanRead = true;
            editor.ProjectStructureAccess.AllowedProjectIds.Add(projectId);
        }

        await workspace.SaveAgentAsync(editor);
        return (await workspace.ListAgentsAsync(includeTemplates: false)).Single(agent => agent.Id == agentId);
    }

    // The product's own Project Structure scope (source, project workspace scope, agent access and observed project
    // lifetime). The browser route position is omitted because no circuit publishes a workspace position here.
    private static async Task<IDisposable> ActivateProjectStructureContextAsync(IServiceProvider services,
        IAgentFrameworkWorkspaceService workspace, Guid projectId)
    {
        var admission = Assert.IsType<ProjectWriteAdmission>(
            await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId));
        var product = ProjectStructureAgentChatContextBuilder.BuildScope(AgentChatContextScopeId.Create(), projectId,
            "Live planning smoke", await workspace.ListAgentsAsync(includeTemplates: false), observedProjectAdmission: admission);
        Assert.Equal(AgentChatTrustedSourceKinds.ProjectStructure, product.Source.Kind.Value);
        Assert.Equal(AgentChatContextAccessState.Ready, product.AccessState);
        return services.GetRequiredService<IAgentChatContextRegistry>().ActivateScope(new AgentChatContextScope(
            product.Id, product.Source, product.DisplayName, product.WorkspaceScope, product.AgentAccess, product.AccessMode,
            product.AccessState, surfacePosition: null, product.CompletionRefreshMode, product.ObservedProjectLifetime));
    }

    private static async Task<OwnerCounts> ReadOwnerCountsAsync(IServiceProvider services)
    {
        await using var owner = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        return new OwnerCounts(
            // Projected technical-agent parties are written by the host's startup projection while the smoke runs.
            await owner.Set<Party>().CountAsync(party => party.PartyType != PartyType.AiAgent),
            await owner.Set<WorkforceProfile>().CountAsync(),
            await owner.Set<CapacityBlock>().CountAsync(),
            await owner.Set<ProjectPartyAssignment>().CountAsync(),
            await owner.Set<StaffingRequest>().CountAsync(),
            await owner.Set<PartyOrganizationAffiliation>().CountAsync());
    }

    private static int CountModelRequests(ExecutionRunRecord run) => run.ToolAdmission?.Batches.Length ?? 0;

    [GeneratedRegex(@"sk-[^\s""']+|[A-Za-z0-9_\-\*]{24,}")]
    private static partial Regex SecretLikeToken();

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var redacted = SecretLikeToken().Replace(value, "[redacted]");
        return redacted.Length <= 240 ? redacted : redacted[..240];
    }

    private sealed record OwnerCounts(int Parties, int WorkforceProfiles, int CapacityBlocks, int ProjectAssignments,
        int StaffingRequests, int Affiliations);

    private sealed record LiveTurn(Guid AgentId, Guid ChatSessionId, Guid ExecutionRunId, string AssistantContent,
        DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc, string FailureCategory);

    private sealed record ToolProviderAttachment(string ProviderKey, bool Included, int ToolCount);

    private sealed record RecordedTurn(ExecutionRunDetail Detail, IReadOnlyList<AgentToolProposalRecord> Proposals,
        IReadOnlyList<ToolProviderAttachment> ToolProviders);

    private sealed class LiveHost : IAsyncDisposable
    {
        private readonly CanDoItAllTestEnvironment environment;
        private readonly TestApplication application;
        private readonly AsyncServiceScope scope;
        private readonly List<Guid> chatSessionIds = [];

        private LiveHost(CanDoItAllTestEnvironment environment, TestApplication application)
        {
            this.environment = environment;
            this.application = application;
            scope = application.Services.CreateAsyncScope();
        }

        internal IServiceProvider Services => scope.ServiceProvider;

        internal IAgentFrameworkWorkspaceService Workspace => Services.GetRequiredService<IAgentFrameworkWorkspaceService>();

        // Real production registrations on an isolated disposable PostgreSQL database; nothing is replaced.
        internal static async Task<LiveHost> CreateAsync(string profileKey)
        {
            var environment = CanDoItAllTestEnvironment.Create("integration-live-crmhr-agent-tools");
            try
            {
                var profile = environment.CreatePostgreSqlProfile(profileKey);
                var application = await TestApplication.CreateAsync(new TestHarnessOptions
                {
                    TestEnvironment = environment,
                    ActiveProfile = profile
                });
                return new LiveHost(environment, application);
            }
            catch
            {
                await environment.DisposeAsync();
                throw;
            }
        }

        internal async Task<int> CountModelRequestsAsync()
        {
            await using var reader = application.Services.CreateAsyncScope();
            var workspace = reader.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
            var total = 0;
            foreach (var chatSessionId in chatSessionIds.ToArray())
            {
                total += (await workspace.ListExecutionRunsAsync(new ExecutionRunQuery(ChatSessionId: chatSessionId))).Sum(CountModelRequests);
            }

            return total;
        }

        internal async Task<LiveTurn> SendAsync(Guid agentId, string prompt)
        {
            var session = await Workspace.GetOrCreateChatSessionAsync(agentId);
            chatSessionIds.Add(session.Id);
            var capture = await Services.GetRequiredService<IAgentTurnContextCaptureService>().CaptureAsync(new AgentTurnContextCaptureCommand(
                agentId, session.Id, prompt, AgentExecutionOperationId.New(),
                Services.GetRequiredService<IAgentExecutionProfileGenerationSource>().GetGeneration(), AgentChatExecutionBehavior.Default));
            Assert.NotNull(capture.Authority);
            return await GuardAsync(agentId, session.Id, MinimumModelRequestsPerTurn, token => Workspace.SendMessageAsync(
                agentId, session.Id, capture.Invocation.Prompt, capture.Invocation.Options, token));
        }

        private async Task<LiveTurn> GuardAsync(Guid agentId, Guid chatSessionId, int requiredAllowance,
            Func<CancellationToken, Task<AgentChatRunResult>> turn)
        {
            var before = await CountModelRequestsAsync();
            var allowance = ResolveTurnAllowance(before);
            Assert.True(allowance >= requiredAllowance,
                $"The live execution already used {before} model request(s); another turn would exceed the bound of {MaximumModelRequestsPerExecution}.");
            var startedAtUtc = DateTimeOffset.UtcNow;
            using var timeout = new CancellationTokenSource(TurnTimeout);
            using var stopWatchdog = new CancellationTokenSource();
            var exceeded = false;
            var watchdog = Task.Run(async () =>
            {
                try
                {
                    while (!stopWatchdog.IsCancellationRequested)
                    {
                        await Task.Delay(WatchdogInterval, stopWatchdog.Token);
                        int used;
                        try
                        {
                            used = await CountModelRequestsAsync();
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            // A read that races the run's own write is retried on the next tick.
                            continue;
                        }

                        if (used - before > allowance)
                        {
                            exceeded = true;
                            await timeout.CancelAsync();
                            return;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });
            try
            {
                var result = await turn(timeout.Token);
                return new LiveTurn(agentId, chatSessionId, result.ExecutionRunId, result.AssistantMessage?.Content ?? string.Empty,
                    startedAtUtc, DateTimeOffset.UtcNow, string.Empty);
            }
            catch (AgentChatRunFailedException failure)
            {
                // A run that ends Failed (for example a denied required mutation) is still persisted evidence.
                // The display message is generic for an unclassified failure; the sanitized cause chain names the failure.
                var causes = string.Join(" <- ", CauseChain(failure.InnerException).Select(cause => $"{cause.GetType().Name}: {Sanitize(cause.Message)}"));
                return new LiveTurn(agentId, chatSessionId, failure.ExecutionRunId, string.Empty, startedAtUtc, DateTimeOffset.UtcNow,
                    $"{failure.FailureCategory?.ToString() ?? "Unclassified"}: {Sanitize(failure.SanitizedDisplayMessage)} Causes: {causes}");
            }
            catch (OperationCanceledException) when (exceeded)
            {
                throw new InvalidOperationException(
                    $"The live turn was cancelled because it used more than {allowance} model request(s).");
            }
            finally
            {
                await stopWatchdog.CancelAsync();
                await watchdog;
            }
        }

        private static IEnumerable<Exception> CauseChain(Exception? exception)
        {
            for (var depth = 0; exception is not null && depth < 6; depth++, exception = exception.InnerException)
            {
                yield return exception;
            }
        }

        private static int ResolveTurnAllowance(int used)
            => Math.Min(MaximumModelRequestsPerTurn, MaximumModelRequestsPerExecution - used - CancellationOvershootReserve);

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await application.DisposeAsync();
            await environment.DisposeAsync();
        }
    }

    // Sanitized evidence: identifiers, resolved tool names, decisions, states, counts and UTC times only.
    private sealed class SmokeEvidence(string scenario)
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        private readonly List<object> turns = [];
        private object? provider;

        internal Dictionary<string, object> Targets { get; } = new(StringComparer.Ordinal);

        internal Dictionary<string, object> Observations { get; } = new(StringComparer.Ordinal);

        internal OwnerCounts? CountsBefore { get; set; }

        internal OwnerCounts? CountsAfter { get; set; }

        internal bool Passed { get; set; }

        internal int ModelRequests { get; private set; }

        internal async Task DescribeProviderAsync(LiveHost live, AgentDefinition agent)
        {
            var profile = (await live.Workspace.ListProvidersAsync()).Single(item => item.Id == agent.ProviderProfileId);
            var model = ManagedSeedProviderFallbacks.ResolveModel(agent, profile);
            Assert.Equal(ProviderKind.OpenAi, profile.Kind);
            Assert.Equal(ManagedSeedProviderFallbacks.OpenAiDefaultModel, model);
            Assert.True(live.Services.GetRequiredService<IAgentProviderCredentialResolver>().Resolve(profile).IsResolved,
                "The product credential resolver found no credential for the seeded provider profile.");
            provider = new { kind = profile.Kind, name = profile.Name, transport = profile.Transport, model, agentId = agent.Id, agentName = agent.Name };
        }

        internal async Task<RecordedTurn> RecordTurnAsync(LiveHost live, string label, LiveTurn turn)
        {
            var detail = await live.Workspace.GetExecutionRunDetailAsync(turn.ExecutionRunId);
            var proposals = detail.Run.ToolAdmission?.Batches.SelectMany(batch => batch.Proposals).ToArray() ?? [];
            var toolProviders = ReadToolProviders(detail);
            turns.Add(new
            {
                label,
                executionRunId = detail.Run.Id,
                chatSessionId = turn.ChatSessionId,
                state = detail.Run.State,
                outcome = detail.Run.Outcome,
                failureCategory = turn.FailureCategory,
                providerName = detail.Run.ProviderName,
                model = detail.Run.Model,
                sourceKind = detail.Run.SourceKind,
                admissionSupport = detail.Run.ToolAdmission?.Support,
                modelRequestsInRun = CountModelRequests(detail.Run),
                usageObservations = detail.UsageObservations.Count,
                // The persisted run's own last phases, sanitized: the only record of a run that fails before a model call.
                executionLog = detail.ExecutionLog.OrderBy(item => item.CreatedAtUtc).TakeLast(12).Select(item => new
                {
                    phase = item.Phase,
                    state = item.State,
                    message = Sanitize(item.Message)
                }),
                startedAtUtc = turn.StartedAtUtc,
                completedAtUtc = turn.CompletedAtUtc,
                toolProviders,
                proposals = proposals.Select(item => new
                {
                    proposalId = item.IntentId.Value,
                    toolName = item.Payload.ToolName,
                    effect = item.Payload.Effect,
                    requiresApproval = item.RequiresApproval,
                    state = item.State,
                    approvalStatus = item.ApprovalStatus,
                    approvalId = item.ApprovalId,
                    effectState = item.EffectState
                }),
                pendingApprovals = detail.Run.PendingApprovals.Select(item => new
                {
                    approvalId = item.ApprovalId,
                    toolName = item.ToolName,
                    proposalId = item.ToolAdmission?.IntentId.Value
                }),
                approvals = detail.Approvals.Select(item => new
                {
                    approvalId = item.ApprovalId,
                    toolName = item.ToolName,
                    decision = item.Status,
                    requestedAtUtc = item.RequestedAtUtc,
                    decidedAtUtc = item.DecidedAtUtc
                }),
                toolInvocations = detail.ToolReceipts.OrderBy(item => item.StartedAtUtc).Select(item => new
                {
                    toolName = item.ToolName,
                    providerKey = item.RuntimeToolProviderKey,
                    riskClass = item.RiskClass,
                    outcome = item.InvocationOutcome,
                    effectState = item.EffectState,
                    effectSourceKind = item.EffectSourceKind,
                    effectSourceId = item.EffectSourceId,
                    failureCode = item.FailureCode,
                    completedAtUtc = item.CompletedAtUtc
                })
            });
            return new RecordedTurn(detail, proposals, toolProviders);
        }

        internal async Task WriteAsync(LiveHost live)
        {
            try
            {
                ModelRequests = await live.CountModelRequestsAsync();
            }
            catch (Exception)
            {
                // An unreadable journal must not hide the original failure; the bound assertion then fails closed.
                ModelRequests = -1;
            }

            var directory = Path.Combine(TestRepositoryRoot.Find(), "output", "live-agent-smoke",
                $"{startedAtUtc:yyyyMMddTHHmmssfffZ}");
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "evidence.json"), JsonSerializer.Serialize(new
            {
                scenario,
                passed = Passed,
                startedAtUtc,
                completedAtUtc = DateTimeOffset.UtcNow,
                provider,
                modelRequests = new { used = ModelRequests, bound = MaximumModelRequestsPerExecution },
                targets = Targets,
                countsBefore = CountsBefore,
                countsAfter = CountsAfter,
                observations = Observations,
                turns
            }, Json));
        }

        private static ToolProviderAttachment[] ReadToolProviders(ExecutionRunDetail detail)
        {
            foreach (var observation in detail.UsageObservations.OrderBy(item => item.CreatedAtUtc))
            {
                if (string.IsNullOrWhiteSpace(observation.DiagnosticsJson))
                {
                    continue;
                }

                using var diagnostics = JsonDocument.Parse(observation.DiagnosticsJson);
                if (diagnostics.RootElement.ValueKind != JsonValueKind.Object ||
                    !diagnostics.RootElement.TryGetProperty("contextAssemblyManifest", out var manifest) ||
                    !manifest.TryGetProperty("sources", out var sources))
                {
                    continue;
                }

                return sources.EnumerateArray()
                    .Where(source => source.GetProperty("category").GetString() == AgentRuntimeContextSourceCategories.RuntimeToolProvider)
                    .Select(source => new ToolProviderAttachment(
                        source.GetProperty("sourceId").GetString() ?? string.Empty,
                        source.GetProperty("decision").GetString() == nameof(AgentRuntimeContextSourceDecision.Included),
                        source.GetProperty("itemCount").GetInt32()))
                    .ToArray();
            }

            return [];
        }
    }
}
