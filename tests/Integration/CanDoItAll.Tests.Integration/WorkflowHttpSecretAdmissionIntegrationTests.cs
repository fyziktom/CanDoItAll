using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Security;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed partial class WorkflowHttpSecretAdmissionIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostgreSql_ApprovedOriginalVersionUsesSecretAfterNativeRequestRoundTrip(bool legacyOccurrence) {
        await using var fixture = await Fixture.CreateAsync(legacyOccurrence);
        await using (var database = await fixture.Factory.CreateDbContextAsync()) {
            var later = fixture.Definition with {
                VersionId = WorkflowVersionId.New(),
                Graph = fixture.Definition.Graph with {
                    Nodes = [fixture.Node with { Settings = fixture.Node.Settings with {
                        ExecutorSettingsJson = WorkflowExecutorJson.Serialize(fixture.Settings with {
                            SecretHeader = fixture.Settings.SecretHeader with { SecretId = Guid.NewGuid() }
                        })
                    } }]
                }
            };
            database.Add(WorkflowDefinitionRecord.FromDefinition(later, revision: 2));
            await database.SaveChangesAsync();
        }

        var result = await fixture.InvokeAsync();

        var request = await fixture.Server.Request;
        Assert.Contains("Authorization: Bearer admission-secret", request, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("admission-secret", result.PayloadJson, StringComparison.Ordinal);
        Assert.Equal(1, fixture.Vault.ReadCount);
        Assert.NotNull(fixture.Headers.Context!.ApprovalAdmission);
        Assert.Equal(legacyOccurrence, fixture.Headers.Context.ExecutionOccurrence is null);
        var json = JsonSerializer.Serialize(fixture.Headers.Context, Fixture.JsonOptions);
        Assert.DoesNotContain("approvalAdmission", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("responseLease", json, StringComparison.OrdinalIgnoreCase);
        await fixture.AssertPendingUnchangedAsync();
    }

    [Theory]
    [InlineData(InvocationChange.Input)]
    [InlineData(InvocationChange.Secret)]
    [InlineData(InvocationChange.Purpose)]
    [InlineData(InvocationChange.Destination)]
    [InlineData(InvocationChange.Version)]
    [InlineData(InvocationChange.Occurrence)]
    [InlineData(InvocationChange.MissingAdmission)]
    [InlineData(InvocationChange.ActualRequestDestination)]
    [InlineData(InvocationChange.ActualRequestUserInfo)]
    public async Task PostgreSql_ActualInvokerRejectsChangedApprovedInvocationBeforeVaultOrHttp(InvocationChange change) {
        await using var fixture = await Fixture.CreateAsync();
        var definition = fixture.Definition;
        var node = fixture.Node;
        var input = fixture.Input;
        var invocation = fixture.Invocation;
        switch (change) {
            case InvocationChange.Input:
                input = input with { PayloadJson = "{\"destination\":\"https://different.invalid\"}" };
                break;
            case InvocationChange.Secret:
                node = fixture.WithSettings(fixture.Settings with {
                    SecretHeader = fixture.Settings.SecretHeader with { SecretId = Guid.NewGuid() }
                });
                break;
            case InvocationChange.Purpose:
                node = fixture.WithSettings(fixture.Settings with {
                    SecretHeader = fixture.Settings.SecretHeader with { Purpose = SecretRuntimePurposes.StorageCredential }
                });
                break;
            case InvocationChange.Destination:
                node = fixture.WithSettings(fixture.Settings with { Url = "https://different.invalid" });
                break;
            case InvocationChange.Version:
                definition = definition with { VersionId = WorkflowVersionId.New() };
                break;
            case InvocationChange.Occurrence:
                invocation = invocation with { ExecutionOccurrence = WorkflowExecutionOccurrence.Start(fixture.Run.RunId) };
                break;
            case InvocationChange.MissingAdmission:
                invocation = invocation with { ApprovalAdmission = null };
                break;
            case InvocationChange.ActualRequestDestination:
                fixture.Headers.BeforeApply = (_, request) => request.RequestUri = new Uri("https://different.invalid");
                break;
            case InvocationChange.ActualRequestUserInfo:
                fixture.Headers.BeforeApply = (_, request) => request.RequestUri = new UriBuilder(request.RequestUri!) {
                    UserName = "unapproved-user", Password = "unapproved-password"
                }.Uri;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change));
        }

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => fixture.InvokeAsync(definition, node, input, invocation));

        Assert.DoesNotContain("admission-secret", exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, fixture.Vault.ReadCount);
        Assert.False(fixture.Server.Request.IsCompleted);
        await fixture.AssertPendingUnchangedAsync();
    }

    [Theory]
    [InlineData(OwnerChange.CancelledRun)]
    [InlineData(OwnerChange.ConsumedResponse)]
    [InlineData(OwnerChange.ReplacedLease)]
    [InlineData(OwnerChange.MissingSecret)]
    [InlineData(OwnerChange.ExpiredApproval)]
    [InlineData(OwnerChange.ReplacedSource)]
    public async Task PostgreSql_OwnerRechecksCurrentStateAfterApprovalBeforeVaultOrHttp(OwnerChange change) {
        await using var fixture = await Fixture.CreateAsync();
        if (change == OwnerChange.ExpiredApproval) {
            fixture.Clock.Now = fixture.Operation.AcceptedAtUtc.AddSeconds(WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds);
        } else if (change == OwnerChange.MissingSecret) {
            await using var security = await fixture.SecurityFactory.CreateDbContextAsync();
            var secret = await security.Set<SecretRecord>().SingleAsync(row => row.Id == fixture.SecretId);
            security.Remove(secret);
            await security.SaveChangesAsync();
        } else {
            await using var database = await fixture.Factory.CreateDbContextAsync();
            if (change == OwnerChange.CancelledRun) {
                var run = await database.Set<WorkflowRunRecordEntity>().SingleAsync(row => row.RunId == fixture.Run.RunId.Value);
                run.State = WorkflowRunState.Cancelled;
            } else if (change == OwnerChange.ConsumedResponse) {
                var request = await database.Set<WorkflowExternalRequestRecordEntity>().SingleAsync(row => row.Id == fixture.Request.Id.Value);
                request.RespondedAtUtc = fixture.Clock.Now;
            } else if (change == OwnerChange.ReplacedSource) {
                var run = await database.Set<WorkflowRunRecordEntity>().SingleAsync(row => row.RunId == fixture.Run.RunId.Value);
                run.OriginJson = JsonSerializer.Serialize(new WorkflowLaunchOrigin.Preview(
                    new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "different-source"),
                    new WorkflowLaunchCorrelationId("different-continuation")) {
                    AuthorizationScope = fixture.Run.Origin!.AuthorizationScope,
                    AuthorizationPolicyFingerprint = fixture.Run.Origin.AuthorizationPolicyFingerprint
                }, Fixture.JsonOptions);
            } else {
                var operation = await database.Set<WorkflowExternalResponseOperationEntity>().SingleAsync(row => row.Id == fixture.Operation.Id.Value);
                operation.LeaseOwnerId = "different-current-owner";
                operation.LeaseEpoch++;
            }
            await database.SaveChangesAsync();
        }

        await Assert.ThrowsAnyAsync<Exception>(() => fixture.InvokeAsync());

        Assert.Equal(0, fixture.Vault.ReadCount);
        Assert.False(fixture.Server.Request.IsCompleted);
    }

    [Fact]
    public async Task PostgreSql_ApprovalExpiresDuringVaultRead_NoHeaderOrHttpIsReleased() {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Vault.BeforeReturn = () => fixture.Clock.Now = fixture.Operation.AcceptedAtUtc
            .AddSeconds(WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds);

        await Assert.ThrowsAnyAsync<Exception>(() => fixture.InvokeAsync());

        Assert.Equal(1, fixture.Vault.ReadCount);
        Assert.False(fixture.Headers.SecretHeaderWasApplied);
        Assert.False(fixture.Server.Request.IsCompleted);
        await fixture.AssertPendingUnchangedAsync();
    }

    [Fact]
    public async Task PostgreSql_CurrentSourceDenialKeepsOriginalScopeAndPreventsSecretRelease() {
        WorkflowStructureAuthority? source = null;
        await using var fixture = await Fixture.CreateAsync(originFactory: (_, profileId) => {
            var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "launch-user");
            source = new WorkflowStructureAuthority(WorkflowStructureAuthorityChannel.LocalOperator,
                actor, profileId, Guid.Empty, false, false, null, "source-policy") {
                AllProjects = true, ProjectScope = new([])
            };
            return new WorkflowLaunchOrigin.Preview(actor, new WorkflowLaunchCorrelationId("approved-http-test")) {
                StructureAuthority = source
            };
        });
        var policy = new DenyingSourcePolicy(source!);
        fixture.Headers.Inner = fixture.CreateOwner(sourcePolicy: policy);

        await Assert.ThrowsAnyAsync<Exception>(() => fixture.InvokeAsync());

        Assert.Equal(1, policy.Checks);
        Assert.Equal(0, fixture.Vault.ReadCount);
        Assert.False(fixture.Server.Request.IsCompleted);
    }

    [Fact]
    public async Task PostgreSql_RuntimeProfileFenceRejectsStaleGenerationBeforeVaultRead() {
        await using var fixture = await Fixture.CreateAsync();
        var canonical = fixture.Application.Services.GetRequiredService<ICanonicalRuntimeDatabase>();
        fixture.Headers.Inner = fixture.CreateOwner(canonical: new DifferentGeneration(canonical));

        await Assert.ThrowsAnyAsync<Exception>(() => fixture.InvokeAsync());

        Assert.Equal(0, fixture.Vault.ReadCount);
        Assert.False(fixture.Server.Request.IsCompleted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostgreSql_MappedProcessWithoutStructureUsesItsOriginalOwnerPort(bool admitted) {
        var (fixture, parent) = await CreateMappedFixtureAsync(legacy: false);
        await using var lifetime = fixture;
        if (!admitted) {
            await parent.CancelAsync();
        }
        if (admitted) {
            await fixture.InvokeAsync();
            Assert.Equal(1, fixture.Vault.ReadCount);
            Assert.Contains("Bearer admission-secret", await fixture.Server.Request, StringComparison.Ordinal);
        } else {
            await Assert.ThrowsAnyAsync<Exception>(() => fixture.InvokeAsync());
            Assert.Equal(0, fixture.Vault.ReadCount);
            Assert.False(fixture.Server.Request.IsCompleted);
        }
        await fixture.AssertPendingUnchangedAsync();
    }

    public enum InvocationChange { Input, Secret, Purpose, Destination, Version, Occurrence, MissingAdmission, ActualRequestDestination, ActualRequestUserInfo }
    public enum OwnerChange { CancelledRun, ConsumedResponse, ReplacedLease, MissingSecret, ExpiredApproval, ReplacedSource }

    private sealed class Fixture : IAsyncDisposable {
        internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        public required TestApplication Application { get; init; }
        public required AsyncServiceScope Scope { get; init; }
        public required Clock Clock { get; init; }
        public required Vault Vault { get; init; }
        public required HttpServer Server { get; init; }
        public required Guid SecretId { get; init; }
        public required WorkflowDefinition Definition { get; init; }
        public required WorkflowNode Node { get; init; }
        public required WorkflowHttpExecutorSettings Settings { get; init; }
        public required WorkflowNodeInput Input { get; init; }
        public required WorkflowRunSnapshot Run { get; init; }
        public required WorkflowExternalRequestRecord Request { get; init; }
        public required WorkflowExternalResponseOperationRecord Operation { get; init; }
        public required WorkflowExecutorInvocationContext Invocation { get; init; }
        public required HeaderProbe Headers { get; init; }
        public IDbContextFactory<WorkflowDbContext> Factory => Scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkflowDbContext>>();
        public IDbContextFactory<SecurityDbContext> SecurityFactory => Scope.ServiceProvider.GetRequiredService<IDbContextFactory<SecurityDbContext>>();

        public static async Task<Fixture> CreateAsync(bool legacyOccurrence = false,
            Func<WorkflowDefinition, Guid, WorkflowLaunchOrigin>? originFactory = null,
            Func<IServiceProvider, WorkflowDefinition, Task<(WorkflowLaunchOrigin Origin, string InputJson)>>? originBuilder = null,
            Func<WorkflowHttpExecutorSettings, WorkflowHttpExecutorSettings>? settingsFactory = null, bool captureDisclosure = false) {
            var clock = new Clock();
            var vault = new Vault();
            var application = await TestApplication.CreateAsync(new TestHarnessOptions {
                ConfigureServices = services => {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(clock);
                    services.RemoveAll<ISecretVault>();
                    services.AddSingleton<ISecretVault>(vault);
                }
            });
            var scope = application.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var server = new HttpServer();
            var secretId = Guid.NewGuid();
            var saved = await services.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
                Id = secretId, Name = "workflow-http-admission", SecretValue = "admission-secret", Scope = "workflow-test"
            });
            Assert.True(saved.IsSuccess);
            var settings = new WorkflowHttpExecutorSettings { UrlJsonPath = "$.destination", AllowPrivateNetworkTargets = true,
                SecretHeader = new WorkflowHttpSecretHeaderBinding { SecretId = secretId } };
            if (originBuilder is not null) {
                settings = settings with { Url = server.Url, UrlJsonPath = string.Empty };
            }
            settings = settingsFactory?.Invoke(settings) ?? settings;
            var node = new WorkflowNode(new WorkflowNodeId("http"), WorkflowNodeKind.Executor, "HTTP", [],
                new WorkflowNodeSettings(null, null, null, null, string.Empty, WorkflowValueShape.Text, WorkflowValueShape.Text) {
                    ExecutorId = WorkflowExecutorIds.HttpFetch, ExecutorSettingsJson = WorkflowExecutorJson.Serialize(settings),
                    ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
                });
            var definition = new WorkflowDefinition(WorkflowId.New(), WorkflowVersionId.New(), "Credential admission", "Synthetic test",
                WorkflowLifecycleStatus.Draft, new WorkflowGraph(node.Id, [node], []),
                new WorkflowRuntimePolicy(WorkflowRuntimeBackendKind.InProcess, true, false, false, false), clock.Now.AddMinutes(-2), clock.Now.AddMinutes(-2));
            if (originBuilder is not null) {
                definition = definition with { Status = WorkflowLifecycleStatus.Active };
            }
            var profileId = services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id;
            var authorityScope = WorkspaceScopeDescriptor.Organization(profileId.ToString("N"));
            var originActor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "launch-user");
            (WorkflowLaunchOrigin Origin, string InputJson)? preparedSource = originBuilder is null ? null : await originBuilder(services, definition);
            var proposedOrigin = preparedSource?.Origin ?? originFactory?.Invoke(definition, profileId) ??
                new WorkflowLaunchOrigin.Preview(originActor, new WorkflowLaunchCorrelationId("approved-http-test"));
            authorityScope = proposedOrigin.AuthorizationScope ?? authorityScope;
            var origin = proposedOrigin with {
                AuthorizationScope = authorityScope, AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint
            };
            var policyOriginActor = origin switch {
                WorkflowLaunchOrigin.Preview preview => preview.Actor,
                WorkflowLaunchOrigin.Api api => api.Actor,
                _ => null
            };
            var run = new WorkflowRunSnapshot(WorkflowRunId.New(), definition.Id, definition.VersionId, WorkflowRunState.WaitingForInput,
                WorkflowRuntimeBackendKind.InProcess, "admission-backend", "Waiting for approval", clock.Now.AddMinutes(-2), clock.Now.AddMinutes(-1)) { Origin = origin };
            var input = new WorkflowNodeInput(preparedSource?.InputJson ?? JsonSerializer.Serialize(new { destination = server.Url }, JsonOptions)) {
                ExecutionOccurrence = legacyOccurrence ? null : WorkflowExecutionOccurrence.Start(run.RunId)
            };
            var requestId = WorkflowExternalRequestId.New();
            var descriptor = BuiltInWorkflowExecutorDescriptors.HttpFetch;
            var request = new WorkflowExternalRequestRecord(requestId, run.RunId, WorkflowExternalRequestKind.Approval, node.Id,
                "ApprovalRequested", "{}", string.Empty, clock.Now.AddMinutes(-1), null) {
                Version = WorkflowExternalRequestVersion.Initial, State = WorkflowExternalRequestState.Pending,
                ResponseContract = new WorkflowExternalResponseContract(WorkflowExternalRequestKind.Approval,
                    "CanDoItAll.WorkflowApprovalResponse/v1", 1, "{}", 1024),
                Continuation = new WorkflowExternalRequestContinuation(new WorkflowBackendExternalRequestLink(requestId,
                    new WorkflowBackendRequestId("approved-native-request"), new WorkflowBackendRequestPortId("http-approval")),
                    new WorkflowBackendCheckpointLink(new WorkflowBackendSessionId("approved-session"), new WorkflowBackendCheckpointId("approved-checkpoint")),
                    new WorkflowCompilerContractVersion(1), WorkflowTopologyFingerprint.Create("approved-http-topology"),
                    WorkflowBackendCheckpointPayloadHash.Compute("{}")),
                AuthorizationPolicy = new WorkflowExternalRequestAuthorizationPolicySnapshot(policyOriginActor, descriptor.Id,
                    descriptor.PermissionPolicy.RequiredCapabilities, descriptor.PermissionPolicy.ApprovalRequirement, string.Empty) {
                    AuthorizationScope = authorityScope, AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                    ResponseAuthorizationLifetimeSeconds = WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds
                }
            };
            Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var boundary));
            var factory = services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>();
            await using (var database = await factory.CreateDbContextAsync()) {
                database.Add(WorkflowDefinitionRecord.FromDefinition(definition, 1));
                if (captureDisclosure) {
                    await database.SaveChangesAsync();
                    await services.GetRequiredService<IWorkflowRunStore>().CreateRunWithStartedEventAsync(run,
                        new(Guid.NewGuid(), run.RunId, WorkflowEventKind.Started, null, "HTTP original admission", "{}", run.CreatedAtUtc) {
                            DisclosureDeclaration = new(run.RunId, definition.Id, definition.VersionId,
                                WorkflowProviderDisclosureContent.Definition(definition), WorkflowProviderDisclosureContent.Source(origin),
                                WorkflowProviderDisclosureProtocol.Current)
                        });
                } else {
                    database.Add(WorkflowRunRecordEntity.FromSnapshot(run));
                }
                database.Add(WorkflowExternalRequestRecordEntity.FromRequest(request));
                var entity = new WorkflowExternalRequestBoundaryEntity { RequestId = request.Id.Value };
                PersistentWorkflowExternalRequestBoundaryStore.Apply(entity, boundary!);
                database.Add(entity);
                await database.SaveChangesAsync();
            }
            var boundaries = services.GetRequiredService<PersistentWorkflowExternalRequestBoundaryStore>();
            var pendingBoundary = await boundaries.ReadAsync(request.Id);
            Assert.Equal(WorkflowExternalRequestBoundaryReadOutcome.Found, pendingBoundary.Outcome);
            Assert.Equal(boundary, pendingBoundary.Boundary);
            var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "approver");
            var fingerprint = WorkflowExternalResponseFingerprintFactory.Create(request.Id, request.Version, actor, authorityScope,
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint, new WorkflowExternalResponseIdempotencyKey("approved-http-response"), "{\"approved\":true}");
            var operations = services.GetRequiredService<PersistentWorkflowExternalResponseOperationStore>();
            var created = await operations.CreateOrReplayAsync(new(WorkflowExternalResponseOperationId.New(), request.Id, run.RunId, request.Version,
                fingerprint, actor, new WorkflowLaunchCorrelationId("response-correlation"), clock.Now));
            Assert.True(created.Succeeded);
            var claimed = await operations.TryClaimAsync(new(created.Operation!.Id, created.Operation.ConcurrencyVersion,
                new WorkflowExternalResponseLeaseOwnerId("original-host"), clock.Now, clock.Now.AddHours(1), 3));
            Assert.True(claimed.Succeeded);
            var resuming = await operations.TryMarkResumingAsync(new(created.Operation.Id, claimed.Operation!.ConcurrencyVersion,
                claimed.Claim!.Lease.OwnerId, claimed.Claim.Lease.Epoch, clock.Now));
            Assert.True(resuming.Succeeded);
            var operation = resuming.Operation!;
            var claimedBoundary = await boundaries.ReadAsync(request.Id);
            Assert.Equal(WorkflowExternalRequestBoundaryReadOutcome.Found, claimedBoundary.Outcome);
            Assert.Equal(boundary! with { State = WorkflowExternalRequestState.ResponseClaimed }, claimedBoundary.Boundary);
            var authorization = WorkflowExternalResponseAuthorizationFactory.Create(operation, run, request,
                claimedBoundary.Boundary!, WorkflowExternalResponseAction.Approve, clock.Now);
            Assert.True(authorization.Succeeded);
            var native = new MafWorkflowApprovalRequest(WorkflowExecutorApprovalRequestId.New(), WorkflowExecutorApprovalToken.New(), run.RunId,
                definition.Id, definition.VersionId, node.Id, descriptor.Id, descriptor.PermissionPolicy.RequiredCapabilities,
                descriptor.PermissionPolicy.ApprovalRequirement, WorkflowExecutorInputHash.Compute(input), input, "Approve HTTP", "{}");
            var restored = JsonSerializer.Deserialize<MafWorkflowApprovalRequest>(JsonSerializer.Serialize(native, JsonOptions), JsonOptions)!;
            var continuation = MafWorkflowApprovalContinuation.Create(restored, authorization.Authorization!, true, "Reviewed");
            var invocation = continuation.CreateInvocationContext(new WorkflowExecutorInvocationContext {
                ExternalResponseAuthorization = authorization.Authorization, CausationOperationId = operation.Id,
                CausationRequestId = request.Id, CausationRequestVersion = request.Version,
                InvocationGeneration = new WorkflowExecutorInvocationGeneration(request.Version.Value), ResponseLease = operation.Lease
            }, node.Settings.ExecutorSettingsJson) with {
                ExecutionOccurrence = input.ExecutionOccurrence?.Advance(definition.VersionId, node.Id)
            };
            vault.ReadCount = 0;
            return new Fixture { Application = application, Scope = scope, Clock = clock, Vault = vault, Server = server,
                SecretId = secretId, Definition = definition, Node = node, Settings = settings, Input = input, Run = run,
                Request = request, Operation = operation, Invocation = invocation,
                Headers = new HeaderProbe(services.GetRequiredService<WorkflowHttpSecretHeaderApplier>()) };
        }

        public WorkflowNode WithSettings(WorkflowHttpExecutorSettings settings)
            => Node with { Settings = Node.Settings with { ExecutorSettingsJson = WorkflowExecutorJson.Serialize(settings) } };

        public async Task<WorkflowNodeExecutionResult> InvokeAsync(WorkflowDefinition? definition = null, WorkflowNode? node = null,
            WorkflowNodeInput? input = null, WorkflowExecutorInvocationContext? invocation = null) {
            var executor = new HttpFetchWorkflowExecutor(Headers, Scope.ServiceProvider.GetRequiredService<IWorkspaceFileService>());
            var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor], timeProvider: Clock);
            using var run = WorkflowExecutorExecutionAuditScope.Push(Run.RunId, Run.Origin);
            return await invoker.ExecuteAsync(definition ?? Definition, node ?? Node, input ?? Input, invocation ?? Invocation);
        }

        public WorkflowHttpSecretHeaderApplier CreateOwner(IWorkflowStructureSourceAuthorityPolicy? sourcePolicy = null,
            ICanonicalRuntimeDatabase? canonical = null, IWorkflowMappedProcessSourceAuthority? mappedProcessSource = null) => new(Factory,
                Scope.ServiceProvider.GetRequiredService<PersistentWorkflowExternalResponseOperationStore>(),
                canonical ?? Scope.ServiceProvider.GetRequiredService<ICanonicalRuntimeDatabase>(),
                Scope.ServiceProvider.GetRequiredService<IDatabaseRuntimeWriteFence>(),
                Scope.ServiceProvider.GetRequiredService<CoordinatedDatabaseTransaction>(),
                Scope.ServiceProvider.GetRequiredService<ISecretRuntimeResolver>(), Clock, sourcePolicy,
                mappedProcessSource: mappedProcessSource);

        public async Task AssertPendingUnchangedAsync() {
            await using var database = await Factory.CreateDbContextAsync();
            var row = await database.Set<WorkflowExternalResponseOperationEntity>().AsNoTracking().SingleAsync(item => item.Id == Operation.Id.Value);
            Assert.Equal((int)WorkflowExternalResponseOperationState.Resuming, row.State);
            Assert.Equal(Operation.Lease!.OwnerId.Value, row.LeaseOwnerId);
            Assert.Equal(Operation.Lease.Epoch.Value, row.LeaseEpoch);
            Assert.DoesNotContain("admission-secret", row.ProtectedResponsePayload, StringComparison.Ordinal);
        }

        public async ValueTask DisposeAsync() {
            await Server.DisposeAsync();
            await Scope.DisposeAsync();
            await Application.DisposeAsync();
        }
    }

    private sealed class HeaderProbe(IWorkflowHttpSecretHeaderApplier inner) : IWorkflowHttpSecretHeaderApplier {
        public IWorkflowHttpSecretHeaderApplier Inner { get; set; } = inner;
        public WorkflowExecutorExecutionContext? Context { get; private set; }
        public bool SecretHeaderWasApplied { get; private set; }
        public Action<WorkflowExecutorExecutionContext, HttpRequestMessage>? BeforeApply { get; set; }
        public async Task<WorkflowHttpSecretUse> ApplyAsync(WorkflowExecutorExecutionContext context, WorkflowNodeInput input,
            HttpRequestMessage request, CancellationToken cancellationToken = default) {
            Context = context;
            BeforeApply?.Invoke(context, request);
            try {
                return await Inner.ApplyAsync(context, input, request, cancellationToken);
            } finally {
                SecretHeaderWasApplied = request.Headers.Authorization is not null;
            }
        }
    }

    private sealed class Vault : ISecretVault {
        private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
        public int ReadCount { get; set; }
        public Action? BeforeReturn { get; set; }
        public Task SetAsync(string key, string value, CancellationToken ct = default) {
            values[key] = value;
            return Task.CompletedTask;
        }
        public Task<string?> GetAsync(string key, CancellationToken ct = default) {
            ReadCount++;
            BeforeReturn?.Invoke();
            return Task.FromResult(values.GetValueOrDefault(key));
        }
        public Task DeleteAsync(string key, CancellationToken ct = default) {
            values.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class Clock : TimeProvider {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class DifferentGeneration(ICanonicalRuntimeDatabase inner) : ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile => inner.Profile;
        public long Generation => inner.Generation + 1;
    }

    private sealed class DenyingSourcePolicy(WorkflowStructureAuthority expected) : IWorkflowStructureSourceAuthorityPolicy {
        public int Checks { get; private set; }
        public Task<IWorkflowStructureSourceAuthorityLease> AcquireAsync(WorkflowStructureAuthority authority,
            WorkflowStructureAuthorityUse use, WorkflowProjectLifetime? target = null, CancellationToken cancellationToken = default) {
            Checks++;
            Assert.Equal(expected.DatabaseProfileId, authority.DatabaseProfileId);
            Assert.Equal(expected.Principal, authority.Principal);
            Assert.Equal(WorkflowStructureAuthorityUse.Admission, use);
            Assert.Null(target);
            throw new UnauthorizedAccessException("The current original source was revoked.");
        }
    }

    private sealed class HttpServer : IAsyncDisposable {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stopping = new();
        public HttpServer() {
            listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/approved";
            Request = ReceiveAsync();
        }
        private int requestCount;
        public int RequestCount => Volatile.Read(ref requestCount);
        public string Url { get; }
        public string ResponseBody { get; set; } = "{}";
        public string ResponseReason { get; set; } = "OK";
        public int ResponseStatus { get; set; } = 200;
        public Dictionary<string, string> ResponseHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Task<string> Request { get; }
        private async Task<string> ReceiveAsync() {
            using var client = await listener.AcceptTcpClientAsync(stopping.Token);
            Interlocked.Increment(ref requestCount);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var headers = new StringBuilder();
            while (await reader.ReadLineAsync(stopping.Token) is { Length: > 0 } line) {
                headers.AppendLine(line);
            }
            var body = Encoding.UTF8.GetBytes(ResponseBody);
            var response = $"HTTP/1.1 {ResponseStatus} {ResponseReason}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n" +
                string.Join(string.Empty, ResponseHeaders.Select(header => $"{header.Key}: {header.Value}\r\n")) + "\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response), stopping.Token);
            await stream.WriteAsync(body, stopping.Token);
            return headers.ToString();
        }
        public async ValueTask DisposeAsync() {
            await stopping.CancelAsync();
            listener.Stop();
            try {
                await Request;
            } catch (OperationCanceledException) {
            } catch (SocketException) when (stopping.IsCancellationRequested) {
            } catch (ObjectDisposedException) when (stopping.IsCancellationRequested) {
            }
            stopping.Dispose();
        }
    }
}
