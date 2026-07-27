using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class AgentRecruitingEvidencePanelTests
{
    [Fact]
    public void Unbound_candidate_renders_the_bound_agent_empty_state_without_loading_services()
    {
        var evidenceService = new FakeRecruitingEvidenceService();
        using var context = CreateContext(evidenceService, out var workspaceProxy);

        var cut = context.RenderComponent<AgentRecruitingEvidencePanel>();

        Assert.Contains("No bound AgentFramework candidate", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(workspaceProxy.Invocations);
        Assert.Equal(0, evidenceService.TotalCallCount);
    }

    [Fact]
    public void Bound_candidate_loads_application_assessments_and_keeps_readiness_separate_from_activation()
    {
        var fixture = RecruitingEvidenceFixture.Create();
        var evidenceService = new FakeRecruitingEvidenceService
        {
            ExpectedRecruitmentApplicationId = fixture.ApplicationId,
            Interviews = [fixture.Interview],
            Readiness = fixture.Readiness
        };
        using var context = CreateContext(evidenceService, out var workspaceProxy);
        workspaceProxy.Agents = [fixture.Candidate];

        var cut = context.RenderComponent<AgentRecruitingEvidencePanel>(parameters => parameters
            .Add(component => component.CandidateAgentId, fixture.Candidate.Id)
            .Add(component => component.RecruitmentApplicationId, fixture.ApplicationId));

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll("[data-testid='crmhr-assessment-cycle']"));
            Assert.Contains("Needs human review", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Training", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Separate activation required", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Never activates automatically", cut.Markup, StringComparison.Ordinal);
        });

        var request = Assert.Single(evidenceService.ListRequests);
        Assert.Equal(fixture.Candidate.Id, request.CandidateAgentId);
        Assert.Equal(fixture.ApplicationId, request.RecruitmentApplicationId);
        Assert.Contains(nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync), workspaceProxy.Invocations);
        Assert.Contains(nameof(IAgentFrameworkWorkspaceService.ListProvidersAsync), workspaceProxy.Invocations);
        Assert.Empty(
            cut.FindAll("button")
                .Where(button => string.Equals(
                    button.TextContent.Trim(),
                    "Activate",
                    StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Training_recommendation_invokes_the_request_training_callback()
    {
        var fixture = RecruitingEvidenceFixture.Create();
        var evidenceService = new FakeRecruitingEvidenceService
        {
            ExpectedRecruitmentApplicationId = fixture.ApplicationId,
            Interviews = [fixture.Interview],
            Readiness = fixture.Readiness
        };
        using var context = CreateContext(evidenceService, out var workspaceProxy);
        workspaceProxy.Agents = [fixture.Candidate];
        RecruitmentTrainingRequest? trainingRequest = null;

        var cut = context.RenderComponent<AgentRecruitingEvidencePanel>(parameters => parameters
            .Add(component => component.CandidateAgentId, fixture.Candidate.Id)
            .Add(component => component.RecruitmentApplicationId, fixture.ApplicationId)
            .Add(component => component.RequestTraining, request => trainingRequest = request));

        cut.WaitForElement("[data-testid='crmhr-assessment-training']").Click();

        Assert.NotNull(trainingRequest);
        Assert.Equal(fixture.Interview.Id, trainingRequest.InterviewId);
        Assert.Equal(
            Assert.Single(fixture.Interview.Attempts).Id,
            trainingRequest.AttemptId);
        Assert.Equal(
            AgentRecruitingAssessmentClassification.NeedsTraining,
            trainingRequest.Classification);
        Assert.Single(
            cut.FindAll("[data-testid='crmhr-assessment-recheck']"));
    }

    [Fact]
    public void Human_review_is_not_self_asserted_from_the_interactive_page()
    {
        var fixture = RecruitingEvidenceFixture.Create();
        var evidenceService = new FakeRecruitingEvidenceService
        {
            ExpectedRecruitmentApplicationId = fixture.ApplicationId,
            Interviews = [fixture.Interview],
            Readiness = fixture.Readiness
        };
        using var context = CreateContext(evidenceService, out var workspaceProxy);
        workspaceProxy.Agents = [fixture.Candidate];

        var cut = context.RenderComponent<AgentRecruitingEvidencePanel>(parameters => parameters
            .Add(component => component.CandidateAgentId, fixture.Candidate.Id)
            .Add(component => component.RecruitmentApplicationId, fixture.ApplicationId));

        cut.WaitForElement("[data-testid='crmhr-assessment-attempt']");
        Assert.Empty(cut.FindAll("[data-testid='crmhr-assessment-review']"));
        Assert.Contains(
            "Human decisions are accepted only through the authenticated recruiting API",
            cut.Markup,
            StringComparison.Ordinal);
    }

    private static TestContext CreateContext(
        FakeRecruitingEvidenceService evidenceService,
        out RecordingWorkspaceServiceProxy workspaceProxy)
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentRecruitingEvidenceService>(evidenceService);

        var workspaceService =
            DispatchProxy.Create<IAgentFrameworkWorkspaceService, RecordingWorkspaceServiceProxy>();
        workspaceProxy = (RecordingWorkspaceServiceProxy)(object)workspaceService;
        context.Services.AddSingleton(workspaceService);
        return context;
    }

    private sealed record RecruitingEvidenceFixture(
        Guid ApplicationId,
        AgentDefinition Candidate,
        AgentRecruitingInterview Interview,
        AgentRecruitingCandidateReadiness Readiness)
    {
        public static RecruitingEvidenceFixture Create()
        {
            var timestamp = new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);
            var applicationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var candidateId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var interviewId = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var attemptId = Guid.Parse("44444444-4444-4444-4444-444444444444");
            var targetId = Guid.Parse("55555555-5555-5555-5555-555555555555");
            var candidate = new AgentDefinition(
                candidateId,
                "Candidate Atlas",
                "Automation specialist",
                "Candidate used by the CRM-HR evidence panel test.",
                "Complete the assigned assessment.",
                AgentLifecycleStatus.Draft,
                null,
                "test-model",
                AgentWorkloadKind.General,
                AgentChatHistoryMode.FrameworkManaged,
                0.2,
                true,
                false,
                "{}",
                false,
                string.Empty,
                AgentPermissionsPolicy.Default,
                [],
                ["candidate"],
                timestamp,
                timestamp);
            var analysis = new AgentRecruitingAssessmentAnalysis(
                AgentRecruitingAssessmentClassification.NeedsTraining,
                0.86m,
                "The candidate is promising but needs targeted training before recheck.",
                AgentRecruitingProposedNextStep.AssignTraining,
                ["Consistent tool use"],
                ["Escalation judgment"]);
            var attempt = new AgentRecruitingAttempt(
                attemptId,
                interviewId,
                1,
                new AgentRecruitingExecutionTarget(
                    AgentRecruitingTargetKind.WorkflowRun,
                    targetId),
                "crm-hr-triage",
                "v1",
                "v2",
                "sha256:input",
                "sha256:output",
                "crm-hr-assessment",
                "sha256:schema",
                "valid",
                new AgentRecruitingAutomatedEvaluation(
                    AgentRecruitingAutomatedDecision.NeedsHumanReview,
                    82m,
                    null,
                    null,
                    string.Empty,
                    "v2",
                    ["Training recommended"],
                    timestamp),
                AgentRecruitingEvidenceCompleteness.Complete,
                [],
                timestamp,
                analysis);
            var interview = new AgentRecruitingInterview(
                interviewId,
                candidateId,
                "configuration-v1",
                candidate.Name,
                candidate.Model,
                "Validate workflow triage judgment",
                timestamp,
                [attempt],
                [],
                applicationId);
            var readiness = new AgentRecruitingCandidateReadiness(
                candidateId,
                "configuration-v1",
                AgentRecruitingReadinessStatus.AwaitingHumanApproval,
                false,
                false,
                true,
                null,
                null,
                null,
                string.Empty,
                string.Empty,
                ["A qualifying human review is still required."],
                []);
            return new RecruitingEvidenceFixture(
                applicationId,
                candidate,
                interview,
                readiness);
        }
    }

    private sealed class FakeRecruitingEvidenceService : IAgentRecruitingEvidenceService
    {
        public Guid? ExpectedRecruitmentApplicationId { get; init; }

        public IReadOnlyList<AgentRecruitingInterview> Interviews { get; init; } = [];

        public AgentRecruitingCandidateReadiness? Readiness { get; init; }

        public List<ListRequest> ListRequests { get; } = [];

        public int ReadinessCallCount { get; private set; }

        public int TotalCallCount => ListRequests.Count + ReadinessCallCount;

        public Task<AgentRecruitingInterview> CreateInterviewAsync(
            CreateAgentRecruitingInterviewCommand command,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Creating assessments is not expected in these tests.");
        }

        public Task<AgentRecruitingInterview> AppendAttemptAsync(
            Guid interviewId,
            AppendAgentRecruitingAttemptCommand command,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Appending attempts is not expected in these tests.");
        }

        public Task<AgentRecruitingInterview> AppendReviewAsync(
            Guid interviewId,
            AppendAgentRecruitingReviewCommand command,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Appending reviews is not expected in these tests.");
        }

        public Task<AgentRecruitingInterview> GetInterviewAsync(
            Guid interviewId,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Loading a single assessment is not expected in these tests.");
        }

        public Task<IReadOnlyList<AgentRecruitingInterview>> ListCandidateInterviewsAsync(
            Guid candidateAgentId,
            Guid? recruitmentApplicationId = null,
            CancellationToken cancellationToken = default)
        {
            ListRequests.Add(new ListRequest(candidateAgentId, recruitmentApplicationId));
            var result = recruitmentApplicationId == ExpectedRecruitmentApplicationId
                ? Interviews
                : [];
            return Task.FromResult(result);
        }

        public Task<AgentRecruitingCandidateReadiness> GetCandidateReadinessAsync(
            Guid candidateAgentId,
            Guid? recruitmentApplicationId = null,
            CancellationToken cancellationToken = default)
        {
            ReadinessCallCount++;
            Assert.Equal(ExpectedRecruitmentApplicationId, recruitmentApplicationId);
            return Task.FromResult(
                Readiness
                ?? throw new InvalidOperationException("Readiness was not configured for this test."));
        }

        public sealed record ListRequest(
            Guid CandidateAgentId,
            Guid? RecruitmentApplicationId);
    }

    public class RecordingWorkspaceServiceProxy : DispatchProxy
    {
        public IReadOnlyList<AgentDefinition> Agents { get; set; } = [];

        public IReadOnlyList<ProviderProfile> Providers { get; set; } = [];

        public List<string> Invocations { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var methodName = targetMethod?.Name
                ?? throw new InvalidOperationException("Workspace method metadata was not supplied.");
            Invocations.Add(methodName);
            return methodName switch
            {
                nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) =>
                    Task.FromResult(Agents),
                nameof(IAgentFrameworkWorkspaceService.ListProvidersAsync) =>
                    Task.FromResult(Providers),
                "add_ExecutionUpdated" or "remove_ExecutionUpdated" => null,
                _ => throw new InvalidOperationException(
                    $"Workspace service member '{methodName}' was not expected in this component test.")
            };
        }
    }
}
