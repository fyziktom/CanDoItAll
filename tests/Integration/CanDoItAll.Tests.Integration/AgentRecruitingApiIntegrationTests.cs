using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentRecruitingApiIntegrationTests
{
    private static readonly Guid CandidateId = Guid.Parse("97472133-ab8b-4ee6-b7c8-b9567a12ea69");
    private static readonly Guid EvaluatorId = Guid.Parse("43bd4f61-bbdc-4e04-b945-29035ce955cc");
    private static readonly Guid ProviderId = Guid.Parse("151c7f0e-371f-4a28-88a1-f9c5052087f8");
    private static readonly DateTimeOffset EvidenceTime =
        new(2026, 7, 25, 15, 30, 0, TimeSpan.Zero);
    private static readonly string HashA = $"sha256:{new string('a', 64)}";
    private static readonly string HashB = $"sha256:{new string('b', 64)}";

    [Fact]
    public async Task Recruiting_api_round_trips_all_target_kinds_and_never_activates_candidate()
    {
        var resolver = new ConfigurableTargetResolver(CandidateId);
        await using var host = await CreateHostAsync(jwtEnabled: true, resolver);
        ConfigureBearer(host, "reviewer-subject", "Trusted Reviewer");
        var seed = await SeedCatalogAsync(host);
        var interviews = new List<AgentRecruitingInterview>();

        foreach (var kind in Enum.GetValues<AgentRecruitingTargetKind>())
        {
            using var createResponse = await host.Client.PostAsJsonAsync(
                "/api/agent-recruiting/interviews",
                new CreateAgentRecruitingInterviewCommand(
                    CandidateId,
                    seed.CandidateVersion,
                    $"Evidence for {kind}"),
                JsonOptions);
            var created = await ReadAsync<AgentRecruitingInterview>(createResponse);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.Equal(
                $"/api/agent-recruiting/interviews/{created.Id:D}",
                createResponse.Headers.Location?.OriginalString);

            using var attemptResponse = await host.Client.PostAsJsonAsync(
                $"/api/agent-recruiting/interviews/{created.Id:D}/attempts",
                CreateAttemptCommand(
                    new AgentRecruitingExecutionTarget(kind, Guid.NewGuid()),
                    $"challenge-{kind}"),
                JsonOptions);
            var attempted = await ReadAsync<AgentRecruitingInterview>(attemptResponse);

            Assert.Equal(HttpStatusCode.Created, attemptResponse.StatusCode);
            var attempt = Assert.Single(attempted.Attempts);
            Assert.Equal(kind, attempt.Target.Kind);
            Assert.Equal(AgentRecruitingEvidenceCompleteness.Complete, attempt.Completeness);
            interviews.Add(attempted);
        }

        var primary = interviews[0];
        var primaryAttempt = Assert.Single(primary.Attempts);
        using var spoofedReviewResponse = await host.Client.PostAsJsonAsync(
            $"/api/agent-recruiting/interviews/{primary.Id:D}/reviews",
            CreateReviewCommand(primaryAttempt.Id) with
            {
                ReviewerActorId = "spoofed-reviewer"
            },
            JsonOptions);
        var spoofedBody = await spoofedReviewResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, spoofedReviewResponse.StatusCode);
        Assert.Contains(
            "agent-recruiting.reviewer-identity-conflict",
            spoofedBody,
            StringComparison.Ordinal);

        using var reviewResponse = await host.Client.PostAsJsonAsync(
            $"/api/agent-recruiting/interviews/{primary.Id:D}/reviews",
            CreateReviewCommand(primaryAttempt.Id) with
            {
                ReviewerActorId = string.Empty,
                ReviewerDisplayName = "Untrusted request display name"
            },
            JsonOptions);
        var reviewed = await ReadAsync<AgentRecruitingInterview>(reviewResponse);
        var review = Assert.Single(reviewed.Reviews);

        Assert.Equal(HttpStatusCode.Created, reviewResponse.StatusCode);
        Assert.Equal("reviewer-subject", review.ReviewerActorId);
        Assert.Equal("Trusted Reviewer", review.ReviewerDisplayName);
        Assert.True(review.QualifiesForReadiness);

        using var detailResponse = await host.Client.GetAsync(
            $"/api/agent-recruiting/interviews/{primary.Id:D}");
        var detail = await ReadAsync<AgentRecruitingInterview>(detailResponse);
        using var readinessResponse = await host.Client.GetAsync(
            $"/api/agent-recruiting/candidates/{CandidateId:D}/readiness");
        var readiness = await ReadAsync<AgentRecruitingCandidateReadiness>(readinessResponse);

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Single(detail.Attempts);
        Assert.Single(detail.Reviews);
        Assert.Equal(HttpStatusCode.OK, readinessResponse.StatusCode);
        Assert.Equal(seed.CandidateVersion, readiness.CurrentConfigurationVersion);
        Assert.Equal(AgentRecruitingReadinessStatus.Ready, readiness.Status);
        Assert.True(readiness.ReadyForProduction);
        Assert.False(readiness.ActivatesAgent);
        Assert.True(readiness.RequiresSeparateActivationAuthorization);
        Assert.Equal(primary.Id, readiness.QualifyingInterviewId);
        Assert.Equal(primaryAttempt.Id, readiness.QualifyingAttemptId);
        Assert.Equal(review.Id, readiness.QualifyingReviewId);
        Assert.Equal("change-control/CAB-2026-42", readiness.HumanAuthorizationReference);
        Assert.Equal(HashA, readiness.HumanAuthorizationEvidenceHash);
        Assert.Equal(3, readiness.AttemptHistory.Count);
        Assert.Equal(
            Enum.GetValues<AgentRecruitingTargetKind>().Order(),
            resolver.Targets.Select(item => item.Kind).Distinct().Order());

        var persistedCandidate = await LoadCandidateAsync(host);
        Assert.Equal(AgentLifecycleStatus.Draft, persistedCandidate.Status);

        var concurrentRequests = Enumerable.Range(0, 2)
            .Select(index => host.Client.PostAsJsonAsync(
                $"/api/agent-recruiting/interviews/{primary.Id:D}/attempts",
                CreateAttemptCommand(
                    new AgentRecruitingExecutionTarget(
                        AgentRecruitingTargetKind.WorkflowRun,
                        Guid.NewGuid()),
                    $"concurrent-{index}"),
                JsonOptions))
            .ToArray();
        var concurrentResponses = await Task.WhenAll(concurrentRequests);
        try
        {
            Assert.All(
                concurrentResponses,
                response => Assert.True(
                    response.StatusCode is HttpStatusCode.Created
                        or HttpStatusCode.Conflict,
                    $"Unexpected concurrent append status: {response.StatusCode}."));
            var createdCount = concurrentResponses.Count(
                response => response.StatusCode == HttpStatusCode.Created);
            Assert.InRange(createdCount, 1, 2);

            using var persistedResponse = await host.Client.GetAsync(
                $"/api/agent-recruiting/interviews/{primary.Id:D}");
            var persisted = await ReadAsync<AgentRecruitingInterview>(persistedResponse);
            Assert.Equal(1 + createdCount, persisted.Attempts.Count);
            Assert.Equal(
                Enumerable.Range(1, persisted.Attempts.Count),
                persisted.Attempts.Select(item => item.Sequence));
            Assert.Equal(
                persisted.Attempts.Count,
                persisted.Attempts.Select(item => item.Id).Distinct().Count());
        }
        finally
        {
            foreach (var response in concurrentResponses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Recruiting_api_rejects_unknown_mismatched_and_invalid_evidence()
    {
        var resolver = new ConfigurableTargetResolver(CandidateId);
        await using var host = await CreateHostAsync(jwtEnabled: false, resolver);
        var seed = await SeedCatalogAsync(host);

        using var unknownCandidateResponse = await host.Client.PostAsJsonAsync(
            "/api/agent-recruiting/interviews",
            new CreateAgentRecruitingInterviewCommand(
                Guid.NewGuid(),
                seed.CandidateVersion,
                "Unknown candidate"),
            JsonOptions);
        await AssertErrorAsync(
            unknownCandidateResponse,
            HttpStatusCode.NotFound,
            "agent-recruiting.candidate-not-found");

        using var staleVersionResponse = await host.Client.PostAsJsonAsync(
            "/api/agent-recruiting/interviews",
            new CreateAgentRecruitingInterviewCommand(
                CandidateId,
                "stale-candidate-version",
                "Stale candidate"),
            JsonOptions);
        await AssertErrorAsync(
            staleVersionResponse,
            HttpStatusCode.Conflict,
            "agent-recruiting.candidate-version-conflict");

        var interview = await CreateInterviewAsync(host, seed.CandidateVersion);
        using var unknownInterviewResponse = await host.Client.PostAsJsonAsync(
            $"/api/agent-recruiting/interviews/{Guid.NewGuid():D}/attempts",
            CreateAttemptCommand(
                new AgentRecruitingExecutionTarget(
                    AgentRecruitingTargetKind.WorkflowRun,
                    Guid.NewGuid()),
                "unknown-interview"),
            JsonOptions);
        await AssertErrorAsync(
            unknownInterviewResponse,
            HttpStatusCode.NotFound,
            "agent-recruiting.interview-not-found");

        foreach (var kind in Enum.GetValues<AgentRecruitingTargetKind>())
        {
            var missingTargetId = Guid.NewGuid();
            resolver.Set(
                missingTargetId,
                new AgentRecruitingTargetResolution(false, "not-found", false));
            using var missingTargetResponse = await host.Client.PostAsJsonAsync(
                $"/api/agent-recruiting/interviews/{interview.Id:D}/attempts",
                CreateAttemptCommand(
                    new AgentRecruitingExecutionTarget(kind, missingTargetId),
                    $"missing-{kind}"),
                JsonOptions);
            await AssertErrorAsync(
                missingTargetResponse,
                HttpStatusCode.NotFound,
                "agent-recruiting.target-not-found");
        }

        var mismatchId = Guid.NewGuid();
        resolver.Set(
            mismatchId,
            new AgentRecruitingTargetResolution(
                true,
                "Completed",
                true,
                Guid.NewGuid()));
        using var mismatchResponse = await host.Client.PostAsJsonAsync(
            $"/api/agent-recruiting/interviews/{interview.Id:D}/attempts",
            CreateAttemptCommand(
                new AgentRecruitingExecutionTarget(
                    AgentRecruitingTargetKind.AgentExecutionRun,
                    mismatchId),
                "candidate-mismatch"),
            JsonOptions);
        await AssertErrorAsync(
            mismatchResponse,
            HttpStatusCode.Conflict,
            "agent-recruiting.target-candidate-conflict");

        var validTarget = new AgentRecruitingExecutionTarget(
            AgentRecruitingTargetKind.WorkflowRun,
            Guid.NewGuid());
        using var invalidHashResponse = await host.Client.PostAsJsonAsync(
            $"/api/agent-recruiting/interviews/{interview.Id:D}/attempts",
            CreateAttemptCommand(validTarget, "invalid-hash") with
            {
                InputHash = "not-sha-256"
            },
            JsonOptions);
        await AssertErrorAsync(
            invalidHashResponse,
            HttpStatusCode.BadRequest,
            "agent-recruiting.hash-invalid");

        using var rubricConflictResponse = await host.Client.PostAsJsonAsync(
            $"/api/agent-recruiting/interviews/{interview.Id:D}/attempts",
            CreateAttemptCommand(validTarget, "rubric-conflict") with
            {
                AutomatedEvaluation = CreateEvaluation() with
                {
                    RubricVersion = "other-rubric"
                }
            },
            JsonOptions);
        await AssertErrorAsync(
            rubricConflictResponse,
            HttpStatusCode.Conflict,
            "agent-recruiting.rubric-version-conflict");

        using var incompleteTimestampResponse = await host.Client.PostAsJsonAsync(
            $"/api/agent-recruiting/interviews/{interview.Id:D}/attempts",
            CreateAttemptCommand(validTarget, "missing-timestamp") with
            {
                AutomatedEvaluation = CreateEvaluation() with
                {
                    EvaluatedAtUtc = default
                }
            },
            JsonOptions);
        var incompleteInterview =
            await ReadAsync<AgentRecruitingInterview>(incompleteTimestampResponse);
        Assert.Equal(HttpStatusCode.Created, incompleteTimestampResponse.StatusCode);
        var incompleteAttempt = Assert.Single(incompleteInterview.Attempts);
        Assert.Equal(
            AgentRecruitingEvidenceCompleteness.Incomplete,
            incompleteAttempt.Completeness);
        Assert.Contains("evaluation-timestamp", incompleteAttempt.MissingEvidence);

        using var completeAttemptResponse = await host.Client.PostAsJsonAsync(
            $"/api/agent-recruiting/interviews/{interview.Id:D}/attempts",
            CreateAttemptCommand(validTarget, "complete-evidence"),
            JsonOptions);
        var completeInterview = await ReadAsync<AgentRecruitingInterview>(completeAttemptResponse);
        var completeAttempt = completeInterview.Attempts[^1];
        Assert.Equal(AgentRecruitingEvidenceCompleteness.Complete, completeAttempt.Completeness);

        using var missingAuthorizationResponse = await host.Client.PostAsJsonAsync(
            $"/api/agent-recruiting/interviews/{interview.Id:D}/reviews",
            CreateReviewCommand(completeAttempt.Id) with
            {
                AuthorizationReference = string.Empty,
                AuthorizationEvidenceHash = string.Empty
            },
            JsonOptions);
        var reviewed = await ReadAsync<AgentRecruitingInterview>(missingAuthorizationResponse);
        var review = Assert.Single(reviewed.Reviews);
        Assert.Equal(HttpStatusCode.Created, missingAuthorizationResponse.StatusCode);
        Assert.False(review.QualifiesForReadiness);
        Assert.Contains("human-authorization-reference", review.MissingEvidence);
        Assert.Contains("human-authorization-evidence-hash", review.MissingEvidence);

        using var readinessResponse = await host.Client.GetAsync(
            $"/api/agent-recruiting/candidates/{CandidateId:D}/readiness");
        var readiness = await ReadAsync<AgentRecruitingCandidateReadiness>(readinessResponse);
        Assert.Equal(
            AgentRecruitingReadinessStatus.AwaitingHumanApproval,
            readiness.Status);
        Assert.False(readiness.ReadyForProduction);
        Assert.False(readiness.ActivatesAgent);
    }

    [Fact]
    public async Task Recruiting_evidence_is_isolated_by_workspace_profile()
    {
        var firstResolver = new ConfigurableTargetResolver(CandidateId);
        var secondResolver = new ConfigurableTargetResolver(CandidateId);
        await using var first = await CreateHostAsync(jwtEnabled: false, firstResolver);
        await using var second = await CreateHostAsync(jwtEnabled: false, secondResolver);
        var firstSeed = await SeedCatalogAsync(first);
        await SeedCatalogAsync(second);
        var interview = await CreateInterviewAsync(first, firstSeed.CandidateVersion);

        using var hiddenDetailResponse = await second.Client.GetAsync(
            $"/api/agent-recruiting/interviews/{interview.Id:D}");
        await AssertErrorAsync(
            hiddenDetailResponse,
            HttpStatusCode.NotFound,
            "agent-recruiting.interview-not-found");

        using var isolatedReadinessResponse = await second.Client.GetAsync(
            $"/api/agent-recruiting/candidates/{CandidateId:D}/readiness");
        var readiness =
            await ReadAsync<AgentRecruitingCandidateReadiness>(isolatedReadinessResponse);
        Assert.Equal(AgentRecruitingReadinessStatus.NoInterviews, readiness.Status);
        Assert.Empty(readiness.AttemptHistory);
    }

    [Fact]
    public async Task Recruiting_endpoints_inherit_api_authorization()
    {
        await using var host = await CreateHostAsync(jwtEnabled: true);
        var interviewId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();

        var requests = new[]
        {
            new HttpRequestMessage(HttpMethod.Post, "/api/agent-recruiting/interviews")
            {
                Content = JsonContent.Create(
                    new CreateAgentRecruitingInterviewCommand(
                        candidateId,
                        "version",
                        "Protected"))
            },
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/agent-recruiting/interviews/{interviewId:D}/attempts")
            {
                Content = JsonContent.Create(
                    CreateAttemptCommand(
                        new AgentRecruitingExecutionTarget(
                            AgentRecruitingTargetKind.WorkflowRun,
                            Guid.NewGuid()),
                        "protected"))
            },
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/agent-recruiting/interviews/{interviewId:D}/reviews")
            {
                Content = JsonContent.Create(CreateReviewCommand(Guid.NewGuid()))
            },
            new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/agent-recruiting/interviews/{interviewId:D}"),
            new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/agent-recruiting/candidates/{candidateId:D}/readiness")
        };

        try
        {
            foreach (var request in requests)
            {
                using var response = await host.Client.SendAsync(request);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }
        finally
        {
            foreach (var request in requests)
            {
                request.Dispose();
            }
        }
    }

    [Fact]
    public async Task Openapi_exposes_all_recruiting_operations_and_typed_schemas()
    {
        await using var host = await CreateHostAsync(jwtEnabled: false);
        using var document = JsonDocument.Parse(
            await host.Client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");

        var create = paths.GetProperty("/api/agent-recruiting/interviews").GetProperty("post");
        var appendAttempt = paths
            .GetProperty("/api/agent-recruiting/interviews/{interviewId}/attempts")
            .GetProperty("post");
        var appendReview = paths
            .GetProperty("/api/agent-recruiting/interviews/{interviewId}/reviews")
            .GetProperty("post");
        var detail = paths
            .GetProperty("/api/agent-recruiting/interviews/{interviewId}")
            .GetProperty("get");
        var readiness = paths
            .GetProperty("/api/agent-recruiting/candidates/{agentId}/readiness")
            .GetProperty("get");

        AssertRequestSchema(create, "CreateAgentRecruitingInterviewCommand");
        AssertRequestSchema(appendAttempt, "AppendAgentRecruitingAttemptCommand");
        AssertRequestSchema(appendReview, "AppendAgentRecruitingReviewCommand");
        AssertResponseSchema(create, "201", "AgentRecruitingInterview");
        AssertResponseSchema(appendAttempt, "201", "AgentRecruitingInterview");
        AssertResponseSchema(appendReview, "201", "AgentRecruitingInterview");
        AssertResponseSchema(detail, "200", "AgentRecruitingInterview");
        AssertResponseSchema(readiness, "200", "AgentRecruitingCandidateReadiness");
        AssertResponseSchema(create, "400", "ApiErrorResponse");
        AssertResponseSchema(create, "404", "ApiErrorResponse");
        AssertResponseSchema(create, "409", "ApiErrorResponse");
        AssertResponseSchema(appendAttempt, "400", "ApiErrorResponse");
        AssertResponseSchema(appendAttempt, "404", "ApiErrorResponse");
        AssertResponseSchema(appendAttempt, "409", "ApiErrorResponse");
        AssertResponseSchema(appendReview, "400", "ApiErrorResponse");
        AssertResponseSchema(appendReview, "404", "ApiErrorResponse");
        AssertResponseSchema(appendReview, "409", "ApiErrorResponse");
        AssertResponseSchema(detail, "404", "ApiErrorResponse");
        AssertResponseSchema(readiness, "404", "ApiErrorResponse");

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var targetKinds = schemas
            .GetProperty("AgentRecruitingTargetKind")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToList();
        Assert.Equal(
            ["agent-execution-run", "workflow-run", "process-run"],
            targetKinds);
        var readinessProperties = schemas
            .GetProperty("AgentRecruitingCandidateReadiness")
            .GetProperty("properties");
        Assert.True(readinessProperties.TryGetProperty("readyForProduction", out _));
        Assert.True(readinessProperties.TryGetProperty("activatesAgent", out _));
        Assert.True(
            readinessProperties.TryGetProperty(
                "requiresSeparateActivationAuthorization",
                out _));
        Assert.True(readinessProperties.TryGetProperty("attemptHistory", out _));
    }

    private static async Task<ApiTestHost> CreateHostAsync(
        bool jwtEnabled,
        IAgentRecruitingTargetResolver? resolver = null)
        => await ApiTestHost.CreateAsync(
            jwtEnabled,
            services =>
            {
                services.RemoveAll<ILoggerProvider>();
                services.RemoveAll<ISecretVault>();
                services.AddSingleton<ISecretVault, InMemorySecretVault>();
                if (resolver is not null)
                {
                    services.RemoveAll<IAgentRecruitingTargetResolver>();
                    services.AddSingleton(resolver);
                }
            });

    private static void ConfigureBearer(
        ApiTestHost host,
        string subject,
        string displayName)
    {
        var tokenService = host.App.Services.GetRequiredService<IApiTokenService>();
        var token = tokenService.IssueToken(
            new ApiTokenIssueRequest
            {
                Subject = subject,
                DisplayName = displayName
            });
        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);
    }

    private static async Task<SeedResult> SeedCatalogAsync(ApiTestHost host)
    {
        var candidate = CreateAgent(CandidateId, "Recruiting candidate", AgentLifecycleStatus.Draft);
        var evaluator = CreateAgent(EvaluatorId, "Recruiting evaluator", AgentLifecycleStatus.Active);
        var provider = CreateProvider();
        await using var scope = host.App.Services.CreateAsyncScope();
        var catalogStore =
            scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceCatalogStore>();
        await catalogStore.UpdateCatalogAsync(
            catalog => catalog with
            {
                Agents =
                [
                    .. catalog.Agents.Where(
                        item => item.Id != CandidateId && item.Id != EvaluatorId),
                    candidate,
                    evaluator
                ],
                Providers =
                [
                    .. catalog.Providers.Where(item => item.Id != ProviderId),
                    provider
                ]
            });
        return new SeedResult(AgentConfigurationVersion.Create(candidate));
    }

    private static async Task<AgentDefinition> LoadCandidateAsync(ApiTestHost host)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var catalogStore =
            scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceCatalogStore>();
        var catalog = await catalogStore.LoadCatalogAsync();
        return Assert.Single(catalog.Agents, item => item.Id == CandidateId);
    }

    private static async Task<AgentRecruitingInterview> CreateInterviewAsync(
        ApiTestHost host,
        string candidateVersion)
    {
        using var response = await host.Client.PostAsJsonAsync(
            "/api/agent-recruiting/interviews",
            new CreateAgentRecruitingInterviewCommand(
                CandidateId,
                candidateVersion,
                "Adversarial recruiting evidence"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<AgentRecruitingInterview>(response);
    }

    private static AppendAgentRecruitingAttemptCommand CreateAttemptCommand(
        AgentRecruitingExecutionTarget target,
        string challengeKey)
        => new(
            target,
            challengeKey,
            "v1",
            "rubric-v1",
            HashA,
            HashB,
            "contract-v1",
            HashA,
            "succeeded",
            CreateEvaluation());

    private static AgentRecruitingAutomatedEvaluation CreateEvaluation()
        => new(
            AgentRecruitingAutomatedDecision.Passed,
            97m,
            EvaluatorId,
            ProviderId,
            "gpt-test",
            "rubric-v1",
            ["Automated evidence passed."],
            EvidenceTime);

    private static AppendAgentRecruitingReviewCommand CreateReviewCommand(Guid attemptId)
        => new(
            attemptId,
            AgentRecruitingHumanDecision.Approved,
            "reviewer-subject",
            "Trusted Reviewer",
            "change-control/CAB-2026-42",
            HashA,
            "Human authorization granted.");

    private static AgentDefinition CreateAgent(
        Guid id,
        string name,
        AgentLifecycleStatus status)
        => new(
            id,
            name,
            "Evidence specialist",
            "Collects deterministic recruiting evidence.",
            "Evaluate evidence without changing activation state.",
            status,
            ProviderId,
            "gpt-test",
            AgentWorkloadKind.Programming,
            AgentChatHistoryMode.FrameworkManaged,
            0.1,
            false,
            false,
            """{"mode":"evidence"}""",
            false,
            string.Empty,
            AgentPermissionsPolicy.Default,
            [],
            ["recruiting"],
            EvidenceTime,
            EvidenceTime);

    private static ProviderProfile CreateProvider()
        => new(
            ProviderId,
            "Recruiting provider",
            ProviderKind.OpenAi,
            "https://example.invalid",
            "TEST_API_KEY",
            "gpt-test",
            ProviderTransportKind.Responses,
            true,
            true,
            true,
            true,
            false,
            "{}",
            string.Empty,
            "Healthy",
            EvidenceTime,
            [],
            ProviderProfilePurpose.Chat);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<T>(body, JsonOptions)
               ?? throw new InvalidOperationException(
                   $"Response did not deserialize as {typeof(T).Name}: {body}");
    }

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Contains(expectedCode, body, StringComparison.Ordinal);
    }

    private static void AssertRequestSchema(JsonElement operation, string expectedSchema)
    {
        var schema = operation
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        Assert.EndsWith(
            $"/{expectedSchema}",
            schema.GetProperty("$ref").GetString(),
            StringComparison.Ordinal);
    }

    private static void AssertResponseSchema(
        JsonElement operation,
        string statusCode,
        string expectedSchema)
    {
        var schema = operation
            .GetProperty("responses")
            .GetProperty(statusCode)
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        Assert.EndsWith(
            $"/{expectedSchema}",
            schema.GetProperty("$ref").GetString(),
            StringComparison.Ordinal);
    }

    private static JsonSerializerOptions JsonOptions { get; } =
        new(JsonSerializerDefaults.Web);

    private sealed record SeedResult(string CandidateVersion);

    private sealed class ConfigurableTargetResolver(Guid candidateId)
        : IAgentRecruitingTargetResolver
    {
        private readonly Dictionary<Guid, AgentRecruitingTargetResolution> configured = [];
        private readonly object sync = new();

        public List<AgentRecruitingExecutionTarget> Targets { get; } = [];

        public void Set(Guid id, AgentRecruitingTargetResolution resolution)
        {
            lock (sync)
            {
                configured[id] = resolution;
            }
        }

        public Task<AgentRecruitingTargetResolution> ResolveAsync(
            AgentRecruitingExecutionTarget target,
            CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                Targets.Add(target);
                if (configured.TryGetValue(target.Id, out var resolution))
                {
                    return Task.FromResult(resolution);
                }
            }

            return Task.FromResult(
                new AgentRecruitingTargetResolution(
                    true,
                    "Completed",
                    true,
                    target.Kind == AgentRecruitingTargetKind.AgentExecutionRun
                        ? candidateId
                        : null));
        }
    }
}
