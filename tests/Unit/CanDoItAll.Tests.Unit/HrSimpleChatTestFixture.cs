using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Agents.SimpleChats;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.AgentFramework;

internal sealed class HrSimpleChatTestFixture {
    public static DateTimeOffset Now { get; } = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
    public static Guid DefinitionId { get; } = Guid.Parse("c52363d8-9657-4712-ad95-b0753b98bdf1");
    public static Guid ProviderId { get; } = Guid.Parse("dfb361dc-0d1c-4f3b-9427-0ae0b89d8e0e");

    public HrSimpleChatTestFixture(bool mutationAllowed = true, bool provideAuthorityResolver = true,
        WorkspaceScopeDescriptor? sourceScope = null) {
        var catalog = HrSimpleChatToolPolicy.Operations.Select(operation => new CapabilityCatalogItem(
            Guid.NewGuid(), CapabilityKind.Tool, operation.CapabilityKey, operation.CapabilityKey,
            string.Empty, string.Empty, string.Empty, CapabilityProofStatus.Verified,
            string.Empty, Now, IsBuiltIn: true)).ToArray();
        var assignments = catalog.Select(capability => new AgentCapabilityAssignment(capability.Id,
            capability.Key, capability.Kind, capability.ProofStatus, capability.LastVerifiedAtUtc, capability.ProofNotes)).ToArray();
        Agent = new(HrAgentIdentity.AgentId, "HR Agent", "Governance", "Managed HR", "Use approved tools.",
            AgentLifecycleStatus.Active, ProviderId, "model", AgentWorkloadKind.Hr,
            AgentChatHistoryMode.FrameworkManaged, 0.2, false, false, "{}", false,
            HrAgentIdentity.TemplateKey, AgentPermissionsPolicy.Default with { CanUseTools = true }, assignments, [], Now, Now);
        Capabilities = catalog;
        Profile = new(Guid.NewGuid(), "fixture-profile", 7);
        Authority = new(AgentExecutionAuthorityId.Create(), Agent.Id, Profile.ProfileId,
            new(Profile.Generation), sourceScope ?? WorkspaceScopeDescriptor.Sandbox, true, mutationAllowed,
            "fixture-policy", "fixture-policy-fingerprint", Now);
        var sessionReference = new AgentToolSessionReference(Guid.NewGuid(), Guid.NewGuid(), Authority.AuthorityId);
        Session = new(sessionReference, Agent.Id, AgentRuntimeContextPurpose.InteractiveChat,
            new(Profile.ProfileId, Profile.Fingerprint, new(Profile.Generation)));
        Source = new AgentTurnContextReference(new(Guid.NewGuid()), new(Guid.NewGuid()),
            new(sourceScope is null ? "agents" : AgentChatTrustedSourceKinds.ProjectStructure),
            new(sourceScope?.Key ?? "agents"), "agents", "chat", 1, "fixture-context", Now);
        Run = new(sessionReference.ExecutionRunId, Agent.Id, sessionReference.ChatSessionId,
            "HR definition administration", "agents", "agents", string.Empty, string.Empty,
            "operator", "user", ExecutionInvocationMetadata.ApplyContextWorkspaceScope(AgentTurnContextMetadata.Apply("{}", Source, Authority), Authority.WorkspaceScope),
            string.Empty, string.Empty, "Provider", "model", ExecutionState.Running, null,
            Now, Now, Now, null, "diagnostic-runtime-key", null, []);
        var provider = new ProviderProfile(ProviderId, "Provider", ProviderKind.OpenAi, "https://example.test",
            string.Empty, "model", ProviderTransportKind.Responses, true, true, true, true, false,
            "{}", string.Empty, string.Empty, null, [], ProviderProfilePurpose.Chat);
        Context = new(Agent, provider, Capabilities, false, AgentRuntimeToolProviderPurpose.InteractiveChat,
            Run.RuntimeSessionKey, AgentRuntimeContextIntent.Empty with { Purpose = AgentRuntimeContextPurpose.InteractiveChat },
            new Dictionary<string, string>()) {
            Governance = AgentExecutionGovernanceSnapshot.FromAuthority(Authority),
            AdmittedToolSession = sessionReference
        };
        var workspace = DispatchProxy.Create<ISandboxWorkspaceCatalogStore, WorkspaceProxy>();
        ((WorkspaceProxy)(object)workspace).Fixture = this;
        Admissions = new(this);
        LeaseFactory = new(this);
        Scope = new();
        Definitions = new(this);
        Receipts = new(this);
        CurrentAuthority = Authority;
        AuthorityResolver = new(this);
        Authorization = new(workspace, new RunStore(this), Admissions, provideAuthorityResolver ? AuthorityResolver : null);
        Service = new(Definitions, Receipts, new ProviderOptions(), LeaseFactory, Scope, Authorization, Codec);
        Provider = new(Service);
    }

    public AgentDefinition Agent { get; set; }
    public IReadOnlyList<CapabilityCatalogItem> Capabilities { get; set; }
    public AgentExecutionAuthorityRecord Authority { get; }
    public AgentExecutionAuthorityRecord CurrentAuthority { get; set; }
    public AgentTurnContextReference Source { get; }
    public CurrentSourceAuthorityResolver AuthorityResolver { get; }
    public LlmChatRuntimeIdentity Profile { get; }
    public AgentToolSessionAdmission Session { get; set; }
    public ExecutionRunRecord Run { get; set; }
    public AgentRuntimeToolProviderContext Context { get; set; }
    public HrSimpleChatProposalCodec Codec { get; } = new();
    public AdmissionVerifier Admissions { get; }
    public RuntimeLeaseFactory LeaseFactory { get; }
    public OperationScope Scope { get; }
    public DefinitionOwner Definitions { get; }
    public ReceiptOwner Receipts { get; }
    public HrSimpleChatRuntimeAuthorization Authorization { get; }
    public HrSimpleChatAdministration Service { get; }
    public HrSimpleChatRuntimeToolProvider Provider { get; }

    public static CreateLlmChatDefinitionCommand CreateCommand()
        => new("Definition", "Summary", string.Empty, "private system prompt", ProviderId, "model",
            new(0.2, "{\"modelParameters\":{\"maxOutputTokens\":512}}"), TimeSpan.FromSeconds(30),
            new(true, "{\"type\":\"object\",\"properties\":{\"message\":{\"type\":\"string\"}}}", "response", "private schema description"),
            "private revision reason", ["alpha", "beta"]);

    public static LlmChatDefinitionDetails Details(int revision = 1, long token = 0) {
        var command = CreateCommand();
        var id = new LlmChatDefinitionId(DefinitionId);
        return new(new(id, command.Name, command.Summary, command.AvatarImageUrl,
            LlmChatDefinitionStatus.Draft, new(revision), Now, Now, token),
            new(id, new(revision), command.Name, command.Summary, command.AvatarImageUrl, command.SystemPrompt,
                command.ProviderProfileId, ProviderKind.OpenAi, "Provider", command.Model, command.Settings,
                command.Timeout, command.ResponseFormat, Now, command.RevisionReason), command.Tags);
    }

    public void Approve(AgentToolPreparedPayload payload, Guid? intentId = null) {
        Admissions.Invocation = new(Session.Reference, new(Guid.NewGuid()), new(intentId ?? Guid.NewGuid()),
            payload, ExecutionApprovalStatus.Approved, payload.Digest);
    }

    public class WorkspaceProxy : DispatchProxy {
        internal HrSimpleChatTestFixture Fixture { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            return targetMethod?.Name switch {
                nameof(ISandboxWorkspaceCatalogStore.LoadCatalogSnapshotAsync) => Task.FromResult(new SandboxWorkspaceCatalogSnapshot(
                    SandboxWorkspaceCatalog.Empty with { Agents = [Fixture.Agent], Capabilities = Fixture.Capabilities }, new(1))),
                _ => throw new NotSupportedException($"Unexpected workspace operation: {targetMethod?.Name}.")
            };
        }
    }

    public sealed class CurrentSourceAuthorityResolver(HrSimpleChatTestFixture fixture) : IAgentExecutionAuthorityResolver {
        public List<AgentExecutionAuthorityResolutionRequest> Requests { get; } = [];
        public Exception? Failure { get; set; }

        public ValueTask<AgentExecutionAuthorityRecord> ResolveAsync(AgentExecutionAuthorityResolutionRequest request,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if (fixture.LeaseFactory.Active is not { Disposed: false, Current: true }) {
                throw new InvalidOperationException("Current authority must be resolved under the owner profile lease.");
            }

            Requests.Add(request);
            if (Failure is not null) {
                throw Failure;
            }

            return ValueTask.FromResult(fixture.CurrentAuthority);
        }
    }

    public sealed class AdmissionVerifier(HrSimpleChatTestFixture fixture) : IAgentToolAdmissionVerifier {
        public AgentToolAdmittedInvocation? Invocation { get; set; }
        public bool RejectSession { get; set; }
        public int InvocationReads { get; private set; }

        public ValueTask<AgentToolSessionAdmission> RequireSessionAsync(AgentToolSessionReference reference, CancellationToken cancellationToken) {
            if (RejectSession) {
                throw new HrSimpleChatAdministrationException("fixture.session-denied", "The fixture rejects this session.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(fixture.Session);
        }

        public ValueTask<AgentToolAdmittedInvocation> RequireInvocationAsync(AgentToolSessionReference session,
            string toolName, AgentToolSemanticDigest digest, CancellationToken cancellationToken) {
            InvocationReads++;
            return ValueTask.FromResult(Invocation ?? throw new HrSimpleChatAdministrationException(
                "fixture.no-admission", "The fixture has no admitted proposal."));
        }
    }

    private sealed class RunStore(HrSimpleChatTestFixture fixture) : ISandboxWorkspaceExecutionRunStore {
        public Task<ExecutionRunRecord?> GetExecutionRunAsync(Guid executionRunId, CancellationToken cancellationToken)
            => Task.FromResult<ExecutionRunRecord?>(fixture.Run);

        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ExecutionRunDetail?> GetExecutionRunDetailAsync(Guid executionRunId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ExecutionRunDetail> SaveExecutionRunDetailAsync(ExecutionRunDetail detail, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    public sealed class RuntimeLeaseFactory(HrSimpleChatTestFixture fixture) : ILlmChatRuntimeLeaseFactory {
        public RuntimeLease? Active { get; private set; }

        public ValueTask<ILlmChatRuntimeLease> AcquireAsync(CancellationToken cancellationToken) {
            Active = new(fixture.Profile, cancellationToken);
            return ValueTask.FromResult<ILlmChatRuntimeLease>(Active);
        }
    }

    public sealed class RuntimeLease(LlmChatRuntimeIdentity identity, CancellationToken cancellationToken) : ILlmChatRuntimeLease {
        public LlmChatRuntimeIdentity Identity { get; } = identity;
        public CancellationToken CancellationToken { get; } = cancellationToken;
        public bool Current { get; set; } = true;
        public bool Disposed { get; private set; }
        public Result EnsureCurrent() => Current ? Result.Success() : Result.Failure(Error.Failure("Profile changed.", "profile.changed"));

        public ValueTask DisposeAsync() {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    public sealed class OperationScope : ILlmChatOperationScopeAccessor {
        private readonly AsyncLocal<LlmChatOperationExecutionContext?> current = new();
        public LlmChatOperationExecutionContext? Current => current.Value;

        public IDisposable Push(LlmChatOperationExecutionContext context) {
            var previous = current.Value;
            current.Value = context;
            return new Restore(() => current.Value = previous);
        }

        private sealed class Restore(Action restore) : IDisposable {
            public void Dispose() => restore();
        }
    }

    public sealed class DefinitionOwner(HrSimpleChatTestFixture fixture) : ILlmChatDefinitionApplicationService {
        public LlmChatDefinitionDetails Current { get; set; } = Details();
        public Action? OnRead { get; set; }
        public TaskCompletionSource? ReadStarted { get; set; }
        public TaskCompletionSource? ReadGate { get; set; }
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public UpdateLlmChatDefinitionCommand? Update { get; private set; }
        public ChangeLlmChatDefinitionStatusCommand? Status { get; private set; }
        public LlmChatRuntimeIdentity? ObservedProfile { get; private set; }

        public Task<Result<LlmChatDefinitionDetails>> CreateAsync(CreateLlmChatDefinitionCommand command, CancellationToken cancellationToken)
            => throw new NotSupportedException("The HR adapter must never use ordinary create.");

        public Task<Result<LlmChatDefinitionDetails>> UpdateAsync(UpdateLlmChatDefinitionCommand command, CancellationToken cancellationToken) {
            Writes++;
            Update = command;
            return Task.FromResult(Result<LlmChatDefinitionDetails>.Success(Current));
        }

        public Task<Result<LlmChatDefinitionDetails>> ChangeStatusAsync(ChangeLlmChatDefinitionStatusCommand command, CancellationToken cancellationToken) {
            Writes++;
            Status = command;
            return Task.FromResult(Result<LlmChatDefinitionDetails>.Success(Current));
        }

        public async Task<Result<LlmChatDefinitionDetails>> GetAsync(LlmChatDefinitionId definitionId, CancellationToken cancellationToken) {
            Reads++;
            ObservedProfile = fixture.Scope.Current?.RuntimeIdentity;
            ReadStarted?.TrySetResult();
            if (ReadGate is not null) {
                await ReadGate.Task.WaitAsync(cancellationToken);
            }

            OnRead?.Invoke();
            return Result<LlmChatDefinitionDetails>.Success(Current);
        }

        public Task<Result<IReadOnlyList<LlmChatDefinitionDetails>>> ListAsync(LlmChatDefinitionQuery query, CancellationToken cancellationToken)
            => throw new NotSupportedException("The adapter must use the owner's bounded page API.");

        public Task<Result<LlmChatPage<LlmChatDefinitionDetails, LlmChatDefinitionCursor>>> ListPageAsync(
            LlmChatDefinitionQuery query, CancellationToken cancellationToken) {
            ObservedProfile = fixture.Scope.Current?.RuntimeIdentity;
            return Task.FromResult(Result<LlmChatPage<LlmChatDefinitionDetails, LlmChatDefinitionCursor>>.Success(new([Current], null)));
        }
    }

    public sealed class ReceiptOwner(HrSimpleChatTestFixture fixture) : ILlmChatDefinitionCreateReceiptService {
        public CreateLlmChatDefinitionOnceCommand? Command { get; private set; }
        public LlmChatDefinitionCreateKey? LookupKey { get; private set; }
        public LlmChatDefinitionCreateReceipt? SavedReceipt { get; set; }
        public int Lookups { get; private set; }
        public Exception? Failure { get; set; }
        public bool WasReplay { get; set; }
        public Action? AfterCommit { get; set; }
        public int Calls { get; private set; }
        public LlmChatRuntimeIdentity? ObservedProfile { get; private set; }

        public Task<Result<LlmChatDefinitionCreateResponse>> CreateOnceAsync(CreateLlmChatDefinitionOnceCommand command, CancellationToken cancellationToken) {
            Calls++;
            Command = command;
            ObservedProfile = fixture.Scope.Current?.RuntimeIdentity;
            if (Failure is not null) {
                throw Failure;
            }

            AfterCommit?.Invoke();
            var receipt = new LlmChatDefinitionCreateReceipt(command.Key, new(DefinitionId), new(1), 0, Now);
            SavedReceipt = receipt;
            return Task.FromResult(Result<LlmChatDefinitionCreateResponse>.Success(new(receipt, WasReplay)));
        }

        public Task<Result<LlmChatDefinitionCreateReceipt?>> FindReceiptAsync(LlmChatDefinitionCreateKey key, CancellationToken cancellationToken) {
            LookupKey = key;
            Lookups++;
            ObservedProfile = fixture.Scope.Current?.RuntimeIdentity;
            return Task.FromResult(Result<LlmChatDefinitionCreateReceipt?>.Success(SavedReceipt?.Key == key ? SavedReceipt : null));
        }
    }

    private sealed class ProviderOptions : ILlmChatProviderResolver {
        public Task<Result<LlmChatResolvedProvider>> ResolveAsync(Guid providerProfileId, string model,
            AgentReasoningEffortLevel? thinkingEffort, CancellationToken cancellationToken)
            => throw new NotSupportedException("Definition create resolves providers inside the owner.");

        public Task<Result<IReadOnlyList<LlmChatProviderOption>>> ListOptionsAsync(CancellationToken cancellationToken)
            => Task.FromResult(Result<IReadOnlyList<LlmChatProviderOption>>.Success([]));
    }
}
