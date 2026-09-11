using System.Text.Encodings.Web;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.Runtime;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed partial class WorkflowHttpSecretAdmissionIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostgreSql_CredentialTransportStopsBeforeRedirectDestinationWhileOrdinaryRedirectsRemainSupported(bool credentialed) {
        await using var target = new HttpServer { ResponseBody = "{\"destination\":\"ordinary redirect target\"}" };
        await using var fixture = await Fixture.CreateAsync(settingsFactory: settings => settings with {
            SecretHeader = settings.SecretHeader with { SecretId = credentialed ? settings.SecretHeader.SecretId : null }
        });
        fixture.Server.ResponseStatus = 302;
        fixture.Server.ResponseReason = "Found";
        fixture.Server.ResponseBody = string.Empty;
        fixture.Server.ResponseHeaders["Location"] = target.Url;
        if (credentialed) {
            var denied = await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() => fixture.InvokeAsync());
            var cause = Assert.IsType<WorkflowExecutorSanitizedException>(denied.InnerException);
            Assert.Equal(typeof(InvalidOperationException).FullName, cause.OriginalExceptionType);
            Assert.Contains("exact redirect destination", cause.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("admission-secret", denied.ToString(), StringComparison.Ordinal);
            Assert.Equal(0, target.RequestCount);
            Assert.False(target.Request.IsCompleted);
            Assert.Equal(1, fixture.Vault.ReadCount);
        } else {
            var result = await fixture.InvokeAsync();
            Assert.Contains("ordinary redirect target", result.PayloadJson, StringComparison.Ordinal);
            var redirected = await target.Request.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.DoesNotContain("Authorization:", redirected, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, target.RequestCount);
            Assert.Equal(0, fixture.Vault.ReadCount);
        }
        var original = await fixture.Server.Request.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(credentialed, original.Contains("Bearer admission-secret", StringComparison.Ordinal));
        Assert.Equal(1, fixture.Server.RequestCount);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task PostgreSql_MappedHttpReadRechecksOriginalReceivingParentWithoutAnotherEffect(bool cancelParent, bool projectBound) {
        var (fixture, parent) = await CreateMappedFixtureAsync(legacy: false, captureDisclosure: true, projectBound: projectBound);
        await using var lifetime = fixture;
        var services = fixture.Scope.ServiceProvider;
        await using (var parentDatabase = parent.Context()) {
            var savedParent = Assert.Single(await parentDatabase.RuntimeStates.AsNoTracking().ToArrayAsync());
            Assert.Equal(projectBound, savedParent.ProjectAdmissionProjectId is not null);
            Assert.NotNull(savedParent.LaunchAdmissionId);
        }
        var store = services.GetRequiredService<IWorkflowRunStore>();
        var executor = new HttpFetchWorkflowExecutor(fixture.Headers);
        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor], timeProvider: fixture.Clock);
        var binding = new MafWorkflowNodeExecutionBindingFactory(invoker, timeProvider: fixture.Clock);
        using var audit = WorkflowExecutorExecutionAuditScope.Push(fixture.Run.RunId, fixture.Run.Origin);
        using var observer = WorkflowNodeExecutionProgressScope.Push(new MappedDisclosureProgress(store));
        _ = await binding.ExecuteAsync(fixture.Definition, fixture.Node, fixture.Input,
            new Dictionary<WorkflowComponentId, LlmCallComponent>(), new Dictionary<WorkflowNodeId, WorkflowPreviewSimulationStep>(), fixture.Invocation);
        var read = Assert.Single((await store.ReadProviderDisclosureAsync(fixture.Run.RunId)).Completions);
        Assert.Single(read.Evidence);
        if (cancelParent) {
            await parent.CancelAsync();
        }
        var policy = services.GetServices<IWorkflowProviderDisclosurePolicy>().Single(item => item.Owner == WorkflowHttpSecretUse.DisclosureOwner);
        var checking = policy.RequireCurrentAsync(fixture.Run, fixture.Definition, [read]).AsTask();
        if (cancelParent) {
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => checking);
        } else {
            await checking;
        }
        Assert.Equal(1, fixture.Vault.ReadCount);
        var retained = Assert.Single((await store.ReadProviderDisclosureAsync(fixture.Run.RunId)).Completions);
        Assert.Equal(read.Proof.CompletionId, retained.Proof.CompletionId);
        Assert.Equal(read.Evidence[0].PayloadJson, retained.Evidence[0].PayloadJson);
    }

    [Theory]
    [InlineData(false, HttpCredentialFixture.Plain)]
    [InlineData(true, HttpCredentialFixture.Plain)]
    [InlineData(false, HttpCredentialFixture.Quoted)]
    [InlineData(true, HttpCredentialFixture.Quoted)]
    [InlineData(true, HttpCredentialFixture.Solidus)]
    [InlineData(true, HttpCredentialFixture.MixedUnicode)]
    public async Task PostgreSql_ActualHttpRedactsCredentialEchoBeforeResultAndWorkspaceDownload(bool download, HttpCredentialFixture credentialKind) {
        var credential = HttpCredential(credentialKind);
        await using var fixture = await Fixture.CreateAsync(settingsFactory: settings => settings with {
            DownloadToWorkspace = download, OutputPath = "downloads/credential-response.json"
        });
        if (credentialKind != HttpCredentialFixture.Plain) {
            Assert.True((await fixture.Scope.ServiceProvider.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
                Id = fixture.SecretId, Name = "workflow-http-admission", SecretValue = credential, Scope = "workflow-test"
            })).IsSuccess);
        }
        fixture.Server.ResponseBody = HttpResponseBody(credentialKind, "useful response");
        fixture.Server.ResponseReason = $"OK {credential}";
        fixture.Server.ResponseHeaders["Authorization"] = $"Bearer {credential}";
        fixture.Server.ResponseHeaders["X-Echo"] = credential;
        fixture.Server.ResponseHeaders["Set-Cookie"] = "session=transport-only-cookie";
        fixture.Server.ResponseHeaders["X-Safe"] = "visible";

        var result = await fixture.InvokeAsync();

        Assert.Contains($"Bearer {credential}", await fixture.Server.Request.WaitAsync(TimeSpan.FromSeconds(20)), StringComparison.Ordinal);
        AssertCredentialAbsent(result.PayloadJson, credential);
        Assert.DoesNotContain("transport-only-cookie", result.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("useful response", result.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("visible", result.PayloadJson, StringComparison.Ordinal);
        AssertCredentialAbsent(JsonSerializer.Serialize(result.ProviderReadEvidence), credential);
        if (download) {
            var saved = fixture.Scope.ServiceProvider.GetRequiredService<IWorkspaceFileService>().ReadTextFile("downloads/credential-response.json");
            Assert.True(saved.Succeeded);
            AssertCredentialAbsent(JsonSerializer.Serialize(saved), credential);
            Assert.Contains("useful response", JsonSerializer.Serialize(saved), StringComparison.Ordinal);
        }
        Assert.Equal(1, fixture.Vault.ReadCount);
    }

    [Fact]
    public async Task PostgreSql_HttpFailureReasonCannotEchoCredentialIntoDiagnostics() {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Server.ResponseStatus = 500;
        fixture.Server.ResponseReason = "Rejected admission-secret";
        fixture.Server.ResponseBody = "admission-secret";
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => fixture.InvokeAsync());
        Assert.DoesNotContain("admission-secret", exception.ToString(), StringComparison.Ordinal);
        Assert.Contains("500", exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, fixture.Vault.ReadCount);
    }

    [Theory]
    [InlineData(false, HttpDisclosureChange.None, false, HttpCredentialFixture.Plain)]
    [InlineData(true, HttpDisclosureChange.None, false, HttpCredentialFixture.Plain)]
    [InlineData(false, HttpDisclosureChange.ApiSourceRevoked, false, HttpCredentialFixture.Plain)]
    [InlineData(true, HttpDisclosureChange.ApiSourceRevoked, false, HttpCredentialFixture.Plain)]
    [InlineData(false, HttpDisclosureChange.ApprovalExpired, false, HttpCredentialFixture.Plain)]
    [InlineData(true, HttpDisclosureChange.ApprovalExpired, false, HttpCredentialFixture.Plain)]
    [InlineData(true, HttpDisclosureChange.SecretDeleted, false, HttpCredentialFixture.Plain)]
    [InlineData(true, HttpDisclosureChange.MissingPrivateEvidence, false, HttpCredentialFixture.Plain)]
    [InlineData(true, HttpDisclosureChange.None, true, HttpCredentialFixture.Plain)]
    [InlineData(false, HttpDisclosureChange.None, false, HttpCredentialFixture.Quoted)]
    [InlineData(true, HttpDisclosureChange.None, false, HttpCredentialFixture.Quoted)]
    [InlineData(true, HttpDisclosureChange.None, false, HttpCredentialFixture.Solidus)]
    [InlineData(true, HttpDisclosureChange.None, false, HttpCredentialFixture.MixedUnicode)]
    public async Task PostgreSql_NativeHttpApprovalAndRestartRecheckOriginalConsumedReadBeforeLlm(bool restartAfterHttp,
        HttpDisclosureChange change, bool legacy, HttpCredentialFixture credentialKind) {
        var credential = HttpCredential(credentialKind);
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-http-disclosure");
        var profile = environment.CreatePostgreSqlProfile("primary");
        var clock = new Clock();
        var vault = new Vault();
        var port = new HttpDisclosurePort(credential);
        var pause = new HttpDisclosurePause();
        var options = new HttpApiOptions();
        await using var server = new HttpServer { ResponseBody = HttpResponseBody(credentialKind, "approved content") };
        server.ResponseHeaders["X-Echo"] = credential;
        server.ResponseHeaders["Set-Cookie"] = "session=transport-only-cookie";
        WorkflowRunSnapshot started;
        WorkflowExternalRequestRecord request;
        WorkflowExternalResponseOperationId approvedOperation;
        Guid secretId;
        Guid completionId;
        void Configure(IServiceCollection services) {
            services.Configure<ProviderInitializationOptions>(initialization => initialization.SeedDefaults = false);
            foreach (var descriptor in services.Where(item => item.ImplementationType == typeof(ProjectStructureWorkflowDeliveryWorker)).ToArray()) {
                services.Remove(descriptor);
            }
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
            services.RemoveAll<ISecretVault>();
            services.AddSingleton<ISecretVault>(vault);
            services.RemoveAll<IOptionsMonitor<ApiAccessOptions>>();
            services.AddSingleton<IOptionsMonitor<ApiAccessOptions>>(options);
            services.RemoveAll<ILlmInvocationPort>();
            services.AddSingleton<ILlmInvocationPort>(port);
            services.RemoveAll<IProviderRuntimeProfileSource>();
            services.AddSingleton<IProviderRuntimeProfileSource>(new MappedDisclosureProvider());
            services.RemoveAll<IWorkflowProviderInputAdmission>();
            services.AddScoped<IWorkflowProviderInputAdmission>(provider => new HttpDisclosureGate(new WorkflowProviderInputAdmission(
                provider.GetRequiredService<IWorkflowRunStore>(), provider.GetRequiredService<IWorkflowExecutorCatalog>(),
                provider.GetServices<IWorkflowProviderDisclosurePolicy>()), pause));
        }
        await using (var first = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = Configure })) {
            await using var scope = first.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            Assert.False(services.GetRequiredService<IOptions<ProviderInitializationOptions>>().Value.SeedDefaults);
            secretId = Guid.NewGuid();
            Assert.True((await services.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
                Id = secretId, Name = "native-http-disclosure", SecretValue = credential, Scope = "workflow-test"
            })).IsSuccess);
            var shape = new WorkflowValueShape(WorkflowValueShapeKind.Object, "{}", "Reviewed HTTP response");
            var component = await services.GetRequiredService<IWorkflowComponentLibraryService>().SaveComponentAsync(new(null,
                "HTTP summary", null, "mapped-disclosure", WorkflowModality.Text, new(0, 100, false, ""),
                "Summarize the reviewed nonsecret HTTP response.", shape, shape, AgentPermissionsPolicy.Default));
            var start = new WorkflowNode(new("start"), WorkflowNodeKind.Start, "Start", [], new(null, null, null, null, "", shape, shape));
            var http = new WorkflowNode(new("http"), WorkflowNodeKind.Executor, "Approved HTTP", [], new(null, null, null, null, "", shape, shape) {
                ExecutorId = WorkflowExecutorIds.HttpFetch, ExecutorSettingsJson = WorkflowExecutorJson.Serialize(new WorkflowHttpExecutorSettings {
                    Url = server.Url, AllowPrivateNetworkTargets = true, SecretHeader = new() { SecretId = secretId }
                })
            });
            var review = new WorkflowNode(new("review"), WorkflowNodeKind.HumanInput, "Continue approved read", [],
                new(null, null, null, WorkflowExternalRequestKind.HumanInput, "Provide reviewed JSON.", shape, shape));
            var llm = new WorkflowNode(new("provider"), WorkflowNodeKind.LlmCall, "Provider", [], new(component.Id, null, null, null, "", shape, shape));
            var end = new WorkflowNode(new("end"), WorkflowNodeKind.End, "End", [], new(null, null, null, null, "", shape, shape));
            var nodes = restartAfterHttp ? new[] { start, http, review, llm, end } : new[] { start, http, llm, end };
            var edges = nodes.Zip(nodes.Skip(1), (left, right) => new WorkflowEdge(new($"{left.Id.Value}-{right.Id.Value}"),
                left.Id, null, right.Id, null, WorkflowEdgeKind.Direct, "")).ToArray();
            var definition = await services.GetRequiredService<IWorkflowCatalogService>().SaveDefinitionAsync(new(null, null,
                "Native approved HTTP disclosure", "Original approval and consumed response", WorkflowLifecycleStatus.Draft,
                new(start.Id, nodes, edges), new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false)));
            var origin = new WorkflowLaunchOrigin.Api(new(WorkflowLaunchActorKind.User, "http-operator"), new("http-disclosure")) {
                AuthorizationScope = WorkspaceScopeDescriptor.Organization(services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id.ToString("N")),
                AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint
            };
            var startRequest = new WorkflowRunStartRequest(definition.Id, definition.VersionId, "{}", WorkflowRuntimeBackendKind.InProcess, null, null) { Origin = origin };
            started = legacy ? await StartLegacyHttpCheckpointAsync(services, definition, component, startRequest, clock) :
                await services.GetRequiredService<IWorkflowRuntimeManager>().StartAsync(definition, startRequest);
            Assert.Equal(WorkflowRunState.WaitingForInput, started.State);
            var store = services.GetRequiredService<IWorkflowRunStore>();
            request = Assert.Single(await store.ListPendingExternalRequestsAsync(started.RunId), item => item.EffectiveState == WorkflowExternalRequestState.Pending);
            Assert.Equal(WorkflowExternalRequestKind.Approval, request.Kind);
            Assert.Equal(legacy ? WorkflowProviderDisclosureProtocol.Legacy : WorkflowProviderDisclosureProtocol.Current,
                request.Continuation!.CompilerContractVersion);
            Assert.False(server.Request.IsCompleted);
            Assert.Equal(0, vault.ReadCount);
            Assert.Equal(0, server.RequestCount);
            using var approve = JsonDocument.Parse("{\"approved\":true}");
            var pending = services.GetRequiredService<IWorkflowExternalResponseService>().SubmitAsync(new(
                services.GetRequiredService<IWorkflowExternalResponseActorContextFactory>().CreateLocalOperator(), request.Id, request.Version,
                approve.RootElement, new("http-approved"), new("http-approved")));
            if (!restartAfterHttp) {
                try {
                    await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(45));
                    await ApplyHttpDisclosureChangeAsync(services, started.RunId, secretId, change, clock, options);
                } finally {
                    pause.Release.TrySetResult();
                }
            }
            var approved = await pending.WaitAsync(TimeSpan.FromSeconds(45));
            var history = await store.ReadProviderDisclosureAsync(started.RunId);
            approvedOperation = approved.Operation!.Id;
            if (legacy) {
                Assert.Null(history.Declaration);
                Assert.Empty(history.Completions);
                completionId = Guid.Empty;
                Assert.Contains(await store.ListEventsAsync(started.RunId), item => item.NodeId == http.Id && item.Kind == WorkflowEventKind.ExecutorCompleted);
            } else {
                var read = Assert.Single(history.Completions, item => item.Proof.NodeId == http.Id);
                completionId = read.Proof.CompletionId;
                var evidence = JsonSerializer.Deserialize<WorkflowHttpApprovedReadEvidence>(Assert.Single(read.Evidence).PayloadJson,
                    WorkflowProviderDisclosureContent.JsonOptions)!;
                Assert.Equal(approvedOperation.Value, evidence.OperationId);
                Assert.Equal(secretId, evidence.SecretId);
            }
            Assert.Equal(1, vault.ReadCount);
            Assert.Equal(1, server.RequestCount);
            Assert.Contains($"Bearer {credential}", await server.Request.WaitAsync(TimeSpan.FromSeconds(20)), StringComparison.Ordinal);
            await AssertHttpPrivateAndPublicContentAsync(services, started.RunId, credential);
            if (!restartAfterHttp) {
                Assert.Equal(change == HttpDisclosureChange.None ? 1 : 0, port.Calls);
                Assert.Equal(change == HttpDisclosureChange.None, approved.Outcome == WorkflowExternalResponseServiceOutcome.Completed);
                AssertHttpDisclosureFailure(change, pause);
                return;
            }
            Assert.Equal(WorkflowExternalResponseServiceOutcome.WaitingAgain, approved.Outcome);
            Assert.Equal(0, port.Calls);
            request = Assert.Single(await store.ListPendingExternalRequestsAsync(started.RunId), item => item.EffectiveState == WorkflowExternalRequestState.Pending);
            Assert.Equal(WorkflowExternalRequestKind.HumanInput, request.Kind);
        }
        await using var second = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = Configure });
        await using var secondScope = second.Services.CreateAsyncScope();
        var resumedServices = secondScope.ServiceProvider;
        Assert.False(resumedServices.GetRequiredService<IOptions<ProviderInitializationOptions>>().Value.SeedDefaults);
        Assert.Equal(1, vault.ReadCount);
        Assert.Equal(1, server.RequestCount);
        var original = await resumedServices.GetRequiredService<PersistentWorkflowExternalResponseOperationStore>().GetAsync(approvedOperation);
        Assert.NotNull(original);
        Assert.Equal(WorkflowExternalResponseOperationState.WaitingAgain, original.State);
        Assert.Null(original.Lease);
        using var answer = JsonDocument.Parse("{\"reviewed\":true}");
        var resumed = resumedServices.GetRequiredService<IWorkflowExternalResponseService>().SubmitAsync(new(
            resumedServices.GetRequiredService<IWorkflowExternalResponseActorContextFactory>().CreateLocalOperator(), request.Id, request.Version,
            answer.RootElement, new("http-after-restart"), new("http-after-restart")));
        try {
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(45));
            await ApplyHttpDisclosureChangeAsync(resumedServices, started.RunId, secretId, change, clock, options);
        } finally {
            pause.Release.TrySetResult();
        }
        var finished = await resumed.WaitAsync(TimeSpan.FromSeconds(45));
        Assert.Equal(!legacy && change == HttpDisclosureChange.None ? 1 : 0, port.Calls);
        Assert.Equal(!legacy && change == HttpDisclosureChange.None, finished.Outcome == WorkflowExternalResponseServiceOutcome.Completed);
        if (legacy) {
            Assert.NotNull(pause.Failure);
            Assert.Contains("retained legacy Workflow", pause.Failure.Message, StringComparison.Ordinal);
            Assert.Equal(WorkflowExternalResponseOperationState.WaitingAgain,
                (await resumedServices.GetRequiredService<PersistentWorkflowExternalResponseOperationStore>().GetAsync(approvedOperation))!.State);
        } else {
            AssertHttpDisclosureFailure(change, pause);
        }
        Assert.Equal(1, vault.ReadCount);
        Assert.Equal(1, server.RequestCount);
        if (!legacy && change != HttpDisclosureChange.MissingPrivateEvidence) {
            var history = await resumedServices.GetRequiredService<IWorkflowRunStore>().ReadProviderDisclosureAsync(started.RunId);
            Assert.Contains(history.Completions, item => item.Proof.CompletionId == completionId);
        }
        await AssertHttpPrivateAndPublicContentAsync(resumedServices, started.RunId, credential);
    }

    private static async Task<WorkflowRunSnapshot> StartLegacyHttpCheckpointAsync(IServiceProvider services,
        WorkflowDefinition definition, LlmCallComponent component, WorkflowRunStartRequest request, Clock clock) {
        var runId = WorkflowRunId.New();
        var store = services.GetRequiredService<IWorkflowRunStore>();
        var running = new WorkflowRunSnapshot(runId, definition.Id, definition.VersionId, WorkflowRunState.Running,
            WorkflowRuntimeBackendKind.InProcess, runId.ToString(), "Original v1 HTTP run", clock.Now, clock.Now) { Origin = request.Origin };
        await store.CreateRunWithStartedEventAsync(running,
            new(Guid.NewGuid(), runId, WorkflowEventKind.Started, null, "Original v1 producer", "{}", clock.Now));
        var build = services.GetRequiredService<IWorkflowMafCompiler>().Compile(definition, [component], WorkflowPreviewSimulationPlan.Empty,
            new WorkflowExecutorInvocationContext { CompilerContractVersion = WorkflowProviderDisclosureProtocol.Legacy });
        Assert.True(build.Compilation.Succeeded, build.Compilation.ErrorMessage);
        var payloads = services.GetRequiredService<IWorkflowBackendCheckpointPayloadStore>();
        var mapper = new MafWorkflowTurnResultMapper(payloads, new MafWorkflowExternalRequestMapper(clock), new MafWorkflowEventNormalizer(),
            new WorkflowCheckpointFactory(), new WorkflowPayloadPolicyService(), clock);
        var driver = new MafWorkflowNativeStartDriver(payloads, new MafWorkflowStreamingRunDriver(), mapper, clock);
        var started = await driver.StartAsync(definition, request, runId, build, CancellationToken.None);
        await store.SaveRunAsync(started.Run);
        foreach (var item in started.Events) {
            Assert.Null(item.CompletionProof);
            await store.SaveEventAsync(item);
        }
        foreach (var item in started.ExternalRequests) {
            await store.SaveExternalRequestAsync(item);
            Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(item, out var boundary));
            Assert.True((await services.GetRequiredService<IWorkflowExternalRequestBoundaryStore>().UpsertAsync(boundary!)).Succeeded);
        }
        foreach (var item in started.Checkpoints) {
            await store.SaveCheckpointAsync(item);
        }
        return started.Run;
    }

    private static void AssertHttpDisclosureFailure(HttpDisclosureChange change, HttpDisclosurePause pause) {
        if (change == HttpDisclosureChange.None) {
            Assert.Null(pause.Failure);
        } else if (change == HttpDisclosureChange.MissingPrivateEvidence) {
            Assert.NotNull(pause.Failure);
            Assert.Contains("declaration", pause.Failure.Message, StringComparison.OrdinalIgnoreCase);
        } else {
            Assert.NotNull(pause.Failure);
            Assert.Contains("retained HTTP response", pause.Failure.Message, StringComparison.Ordinal);
        }
    }

    private static async Task ApplyHttpDisclosureChangeAsync(IServiceProvider services, WorkflowRunId runId, Guid secretId,
        HttpDisclosureChange change, Clock clock, HttpApiOptions options) {
        switch (change) {
            case HttpDisclosureChange.None:
                return;
            case HttpDisclosureChange.ApiSourceRevoked:
                options.CurrentValue.Enabled = false;
                return;
            case HttpDisclosureChange.ApprovalExpired:
                clock.Now = clock.Now.AddSeconds(WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds);
                return;
            case HttpDisclosureChange.SecretDeleted:
                await services.GetRequiredService<SecretService>().DeleteAsync(secretId);
                Assert.Empty(await services.GetRequiredService<SecretReferenceQuery>().GetExistingIdsAsync([secretId], CancellationToken.None));
                return;
            case HttpDisclosureChange.MissingPrivateEvidence:
                await using (var database = await services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContextAsync()) {
                    var rows = await database.Set<WorkflowEventRecordEntity>().Where(row => row.RunId == runId.Value &&
                        row.Kind == WorkflowEventKind.ProviderReadEvidence).ToArrayAsync();
                    Assert.NotEmpty(rows);
                    database.RemoveRange(rows);
                    await database.SaveChangesAsync();
                }
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(change));
        }
    }

    private static async Task AssertHttpPrivateAndPublicContentAsync(IServiceProvider services, WorkflowRunId runId, string credential) {
        var events = await services.GetRequiredService<IWorkflowRunStore>().ListEventsAsync(runId);
        Assert.DoesNotContain(events, item => item.Kind == WorkflowEventKind.ProviderReadEvidence);
        await using var database = await services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContextAsync();
        var allPayloads = await database.Set<WorkflowEventRecordEntity>().AsNoTracking().Where(row => row.RunId == runId.Value)
            .Select(row => row.PayloadJson).ToArrayAsync();
        foreach (var payload in allPayloads) {
            AssertCredentialAbsent(payload, credential);
            Assert.DoesNotContain("transport-only-cookie", payload, StringComparison.Ordinal);
        }
    }

    public enum HttpCredentialFixture { Plain, Quoted, Solidus, MixedUnicode }

    private static string HttpCredential(HttpCredentialFixture kind) => kind switch {
        HttpCredentialFixture.Plain => "admission-secret",
        HttpCredentialFixture.Quoted => QuotedHttpCredential,
        HttpCredentialFixture.Solidus => "admission/secret",
        HttpCredentialFixture.MixedUnicode => "a\"b/c",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string HttpResponseBody(HttpCredentialFixture kind, string safe) {
        var encoded = kind switch {
            HttpCredentialFixture.Solidus => "\"admission\\/secret\"",
            HttpCredentialFixture.MixedUnicode => "\"\\u0061\\\"b\\/\\u0063\"",
            _ => JsonSerializer.Serialize(HttpCredential(kind), RelaxedHttpJson)
        };
        var response = "{\"value\":" + encoded + ",\"safe\":" + JsonSerializer.Serialize(safe) + "}";
        using var parsed = JsonDocument.Parse(response);
        Assert.Equal(HttpCredential(kind), parsed.RootElement.GetProperty("value").GetString());
        return response;
    }

    private const string QuotedHttpCredential = "admission-\"secret";
    private static readonly JsonSerializerOptions RelaxedHttpJson = new(JsonSerializerDefaults.Web) {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static void AssertCredentialAbsent(string text, string credential) {
        Assert.DoesNotContain(credential, text, StringComparison.Ordinal);
        Assert.DoesNotContain(JsonEncodedText.Encode(credential).ToString(), text, StringComparison.Ordinal);
        Assert.DoesNotContain(JsonEncodedText.Encode(credential, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString(), text, StringComparison.Ordinal);
        JsonDocument document;
        try {
            document = JsonDocument.Parse(text);
        } catch (JsonException) {
            return;
        }
        using (document) {
            Inspect(document.RootElement);
        }
        void Inspect(JsonElement element) {
            if (element.ValueKind == JsonValueKind.String) {
                AssertCredentialAbsent(element.GetString()!, credential);
            } else if (element.ValueKind == JsonValueKind.Object) {
                foreach (var property in element.EnumerateObject()) {
                    AssertCredentialAbsent(property.Name, credential);
                    Inspect(property.Value);
                }
            } else if (element.ValueKind == JsonValueKind.Array) {
                foreach (var item in element.EnumerateArray()) {
                    Inspect(item);
                }
            }
        }
    }

    public enum HttpDisclosureChange { None, ApiSourceRevoked, ApprovalExpired, SecretDeleted, MissingPrivateEvidence }

    private sealed class HttpDisclosurePause {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Exception? Failure { get; set; }
    }

    private sealed class HttpDisclosureGate(IWorkflowProviderInputAdmission inner, HttpDisclosurePause pause) : IWorkflowProviderInputAdmission {
        public async ValueTask RequireAsync(WorkflowDefinition definition, WorkflowNode node, WorkflowNodeInput input,
            CancellationToken cancellationToken = default) {
            pause.Entered.TrySetResult();
            await pause.Release.Task.WaitAsync(TimeSpan.FromSeconds(45), cancellationToken);
            try {
                await inner.RequireAsync(definition, node, input, cancellationToken);
            } catch (Exception exception) {
                pause.Failure = exception;
                throw;
            }
        }
    }

    private sealed class HttpDisclosurePort(string credential) : ILlmInvocationPort {
        public int Calls { get; private set; }
        public Task<LlmInvocationResult> InvokeAsync(LlmInvocationRequest request, CancellationToken cancellationToken = default) {
            Calls++;
            var json = JsonSerializer.Serialize(request);
            AssertCredentialAbsent(json, credential);
            Assert.DoesNotContain("transport-only-cookie", json, StringComparison.Ordinal);
            return Task.FromResult(new LlmInvocationResult(request.Model, "Approved HTTP summary", new(1, 1, 0)));
        }
    }

    private sealed class HttpApiOptions : IOptionsMonitor<ApiAccessOptions> {
        public ApiAccessOptions CurrentValue { get; } = new() { Enabled = true };
        public ApiAccessOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<ApiAccessOptions, string?> listener) => null;
    }
}
