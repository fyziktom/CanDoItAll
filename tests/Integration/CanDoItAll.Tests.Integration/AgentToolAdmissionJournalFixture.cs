using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Agents.SimpleChats;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

internal sealed class AgentToolAdmissionJournalFixture : IAsyncDisposable {
    private readonly CanDoItAllTestEnvironment environment;

    private AgentToolAdmissionJournalFixture(CanDoItAllTestEnvironment environment, string workspaceRoot,
        FileSandboxWorkspaceStore store, AgentToolProfileBinding profile, AgentDefinition agent,
        ProviderProfile provider, ExecutionRunDetail detail) {
        this.environment = environment;
        WorkspaceRoot = workspaceRoot;
        Store = store;
        Profile = profile;
        Agent = agent;
        Provider = provider;
        Detail = detail;
    }

    internal string WorkspaceRoot { get; }
    internal FileSandboxWorkspaceStore Store { get; }
    internal AgentToolProfileBinding Profile { get; }
    internal AgentDefinition Agent { get; }
    internal ProviderProfile Provider { get; }
    internal ExecutionRunDetail Detail { get; }
    internal AgentToolSessionReference Session => Detail.Run.ToolAdmission!.Session.Reference;
    internal AgentToolAdmissionJournal NewJournal(FileSandboxWorkspaceStore? store = null) => new(store ?? Store, Profile);
    internal FileSandboxWorkspaceStore NewStore(Action<ExistingRunDetailCommitStage>? fault = null)
        => new(WorkspaceRoot, WorkspaceScopeDescriptor.Sandbox, chatBackedRunCommitBoundary: null, existingRunDetailCommitBoundary: fault);

    internal static async Task<AgentToolAdmissionJournalFixture> CreateAsync(
        AgentToolAdmissionSupport support = AgentToolAdmissionSupport.Recoverable,
        AgentToolProfileBinding? profileBinding = null, AgentRuntimeTransientContext? transientContext = null,
        bool includeRecoveryInput = false, bool managedHr = false, Func<AgentDefinition, AgentDefinition>? configureAgent = null) {
        var environment = CanDoItAllTestEnvironment.Create($"tool-admission-{Guid.NewGuid():N}");
        try {
            var profile = environment.CreateInMemoryProfile("primary");
            var store = new FileSandboxWorkspaceStore(profile.WorkspaceRootPath, WorkspaceScopeDescriptor.Sandbox);
            var catalog = await store.LoadCatalogSnapshotAsync();
            var agent = catalog.Catalog.Agents.First(item => item.ProviderProfileId.HasValue) with {
                ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged
            };
            if (configureAgent is not null) {
                agent = configureAgent(agent) ?? throw new InvalidOperationException("The journal fixture requires an Agent definition.");
            }
            var provider = new ProviderProfile(agent.ProviderProfileId!.Value, "Journal provider", ProviderKind.OpenAi,
                "https://provider.example.test", string.Empty, "fixture-model", ProviderTransportKind.Responses,
                true, true, true, true, false, "{}", string.Empty, string.Empty, null, [], ProviderProfilePurpose.Chat);
            var binding = profileBinding ?? new AgentToolProfileBinding(Guid.NewGuid(), "journal-fixture-profile", new(1));
            var hrCapabilities = managedHr ? HrSimpleChatToolPolicy.Operations.Select(operation => new CapabilityCatalogItem(
                Guid.NewGuid(), CapabilityKind.Tool, operation.CapabilityKey, operation.CapabilityKey,
                string.Empty, string.Empty, string.Empty, CapabilityProofStatus.Verified, string.Empty,
                DateTimeOffset.UtcNow, IsBuiltIn: true)).ToArray() : [];
            if (managedHr) {
                agent = agent with {
                    Id = HrAgentIdentity.AgentId, Workload = AgentWorkloadKind.Hr, TemplateKey = HrAgentIdentity.TemplateKey,
                    IsTemplate = false, Status = AgentLifecycleStatus.Active,
                    Permissions = agent.Permissions with { CanUseTools = true },
                    Capabilities = hrCapabilities.Select(capability => new AgentCapabilityAssignment(capability.Id,
                        capability.Key, capability.Kind, capability.ProofStatus, capability.LastVerifiedAtUtc, capability.ProofNotes)).ToArray()
                };
            }

            if (includeRecoveryInput) {
                provider = provider with { Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions };
                agent = agent with { IsTemplate = false, Status = AgentLifecycleStatus.Active, Model = provider.DefaultModel };
                await store.UpdateCatalogAsync(current => current with {
                    Agents = current.Agents.Where(item => item.Id != agent.Id).Append(agent).ToArray(),
                    Capabilities = managedHr ? current.Capabilities.Where(item => !hrCapabilities.Any(capability => capability.Key == item.Key))
                        .Concat(hrCapabilities).ToArray() : current.Capabilities,
                    Providers = current.Providers.Where(item => item.Id != provider.Id).Append(provider).ToArray()
                });
            } else if (configureAgent is not null) {
                var saved = await store.UpdateCatalogAsync(current => current with {
                    Agents = current.Agents.Where(item => item.Id != agent.Id).Append(agent).ToArray()
                });
                agent = saved.Agents.Single(item => item.Id == agent.Id);
            }

            var now = DateTimeOffset.UtcNow;
            var admittedScope = transientContext?.WorkspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
            var authority = new AgentExecutionAuthorityRecord(AgentExecutionAuthorityId.Create(), agent.Id, binding.ProfileId,
                binding.Generation, admittedScope, true, true, "fixture-policy", "fixture-authority", now);
            var context = new AgentTurnContextReference(new(Guid.NewGuid()), new(Guid.NewGuid()), new(admittedScope.Kind == WorkspaceScopeKind.Project ? AgentChatTrustedSourceKinds.ProjectStructure : "agents"),
                new(admittedScope.Kind == WorkspaceScopeKind.Project ? admittedScope.Key : "agents"),
                "agents", "chat", 1, "fixture-context", now);
            var runId = Guid.NewGuid();
            var input = new ChatMessageRecord(Guid.NewGuid(), ChatMessageRole.User, "Create the reviewed definitions.", now, 6);
            var chat = new ChatSessionRecord(Guid.NewGuid(), agent.Id, "Durable tool admission", now, now, includeRecoveryInput ? [input] : [],
                LatestExecutionRunId: runId);
            var run = new ExecutionRunRecord(runId, agent.Id, chat.Id, chat.Title, "chat-session", chat.Id.ToString("N"),
                string.Empty, string.Empty, "operator", "user", AgentTurnContextMetadata.Apply("{}", context, authority),
                "Create the reviewed definitions.", string.Empty, provider.Name, provider.DefaultModel, ExecutionState.Preparing,
                null, now, now, now, null, string.Empty, null, [], Revision: 1, ProviderProfileId: provider.Id);
            if (transientContext is not null) {
                run = run with { MetadataJson = ExecutionInvocationMetadata.ApplyTransientContextRequirement(
                    run.MetadataJson, AgentChatContextDigest.Compute(transientContext)) };
            }

            var journal = new AgentToolAdmissionJournal(store, binding);
            run = run with { ToolAdmission = journal.CreateForNewRun(run, chat, support,
                includeRecoveryInput ? new(input.Id, input.Content) : null, transientContext) };
            var detail = await store.SaveExecutionRunDetailAsync(new(run, chat, [], []));
            return new(environment, profile.WorkspaceRootPath, store, binding, agent, provider, detail);
        } catch {
            await environment.DisposeAsync();
            throw;
        }
    }

    internal static void RequireScriptedFault(Exception failure, string expectedMessage) {
        for (Exception? current = failure; current is not null; current = current.InnerException) {
            if (current is IOException && current.Message == expectedMessage) {
                return;
            }
        }
        throw new InvalidOperationException("The provider fixture did not reach its expected fault.", failure);
    }

    internal static AgentToolProtocolEnvelope Envelope(string json = "{}") => AgentToolProtocolEnvelope.Create("fixture-sdk", 1, json);

    internal static AgentToolPreparedPayload Payload(AgentToolProposalRecovery recovery = AgentToolProposalRecovery.OwnerReceipt,
        string value = "alpha") {
        var json = System.Text.Json.JsonSerializer.Serialize(new { value });
        return new("admission_fixture_create", 1, AgentToolProtocolEnvelope.ComputeDigest(json), json,
            recovery == AgentToolProposalRecovery.RevalidateAndRead ? AgentToolProposalEffect.Read : AgentToolProposalEffect.Mutation, recovery);
    }

    internal async Task ApproveAsync(IReadOnlyList<PendingToolApprovalRecord> pending, bool automatic = false) {
        await ((ISandboxWorkspaceExecutionRunMutationStore)Store).UpdateExecutionRunDetailAsync(Session.ExecutionRunId, current => {
            var decisions = pending.Select(item => new PendingToolApprovalDecision(item.ApprovalId, true) { ToolAdmission = item.ToolAdmission }).ToArray();
            var journal = AgentToolJournalTransitions.ApplyDecisions(current.Run.ToolAdmission!, pending, decisions, automatic);
            return current with { Run = current.Run with {
                ToolAdmission = journal, PendingApprovals = [], State = ExecutionState.Running,
                Revision = current.Run.Revision + 1, UpdatedAtUtc = DateTimeOffset.UtcNow
            } };
        });
    }

    public ValueTask DisposeAsync() => environment.DisposeAsync();
}
