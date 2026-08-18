using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentRecruitingEvidenceTests
{
    private static readonly Guid CandidateId = Guid.Parse("a6ee046b-259a-410b-a8f9-3c72d46e9f80");
    private static readonly Guid EvaluatorId = Guid.Parse("692d90ab-b4a6-494f-833c-677da85c9294");
    private static readonly Guid ProviderId = Guid.Parse("bc39244d-287f-425f-94df-ce4f03b79722");
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly string HashA = $"sha256:{new string('a', 64)}";
    private static readonly string HashB = $"sha256:{new string('b', 64)}";

    [Fact]
    public void Project_without_interviews_is_not_ready_and_never_activates_agent()
    {
        var readiness = AgentRecruitingReadinessProjector.Project(
            CandidateId,
            "current-version",
            []);

        Assert.Equal(AgentRecruitingReadinessStatus.NoInterviews, readiness.Status);
        Assert.False(readiness.ReadyForProduction);
        Assert.False(readiness.ActivatesAgent);
        Assert.True(readiness.RequiresSeparateActivationAuthorization);
        Assert.Empty(readiness.AttemptHistory);
    }

    [Fact]
    public void Project_keeps_automated_and_human_decisions_separate()
    {
        var attempt = CreateAttempt(
            sequence: 1,
            completeness: AgentRecruitingEvidenceCompleteness.Complete,
            automatedDecision: AgentRecruitingAutomatedDecision.Passed);
        var interview = CreateInterview("current-version", [attempt]);

        var automatedOnly = AgentRecruitingReadinessProjector.Project(
            CandidateId,
            "current-version",
            [interview]);
        var nonQualifyingApproval = AgentRecruitingReadinessProjector.Project(
            CandidateId,
            "current-version",
            [
                interview with
                {
                    Reviews =
                    [
                        CreateReview(
                            attempt.Id,
                            AgentRecruitingHumanDecision.Approved,
                            qualifiesForReadiness: false)
                    ]
                }
            ]);

        Assert.Equal(
            AgentRecruitingReadinessStatus.AwaitingHumanApproval,
            automatedOnly.Status);
        Assert.Equal(
            AgentRecruitingReadinessStatus.AwaitingHumanApproval,
            nonQualifyingApproval.Status);
        Assert.False(automatedOnly.ReadyForProduction);
        Assert.False(nonQualifyingApproval.ReadyForProduction);
        Assert.False(automatedOnly.ActivatesAgent);
        Assert.False(nonQualifyingApproval.ActivatesAgent);
    }

    [Fact]
    public void Project_requires_complete_current_version_evidence_and_qualifying_authorization()
    {
        var staleAttempt = CreateAttempt(
            sequence: 1,
            completeness: AgentRecruitingEvidenceCompleteness.Complete,
            automatedDecision: AgentRecruitingAutomatedDecision.Passed);
        var staleInterview = CreateInterview(
            "old-version",
            [staleAttempt],
            [
                CreateReview(
                    staleAttempt.Id,
                    AgentRecruitingHumanDecision.Approved,
                    qualifiesForReadiness: true)
            ]);

        var staleReadiness = AgentRecruitingReadinessProjector.Project(
            CandidateId,
            "current-version",
            [staleInterview]);

        Assert.Equal(
            AgentRecruitingReadinessStatus.IncompleteEvidence,
            staleReadiness.Status);
        Assert.False(staleReadiness.ReadyForProduction);
        Assert.False(staleReadiness.ActivatesAgent);
        Assert.Single(staleReadiness.AttemptHistory);

        var currentAttempt = CreateAttempt(
            sequence: 1,
            completeness: AgentRecruitingEvidenceCompleteness.Complete,
            automatedDecision: AgentRecruitingAutomatedDecision.Passed);
        var qualifyingReview = CreateReview(
            currentAttempt.Id,
            AgentRecruitingHumanDecision.Approved,
            qualifiesForReadiness: true);
        var currentInterview = CreateInterview(
            "current-version",
            [currentAttempt],
            [qualifyingReview]);

        var ready = AgentRecruitingReadinessProjector.Project(
            CandidateId,
            "CURRENT-VERSION",
            [staleInterview, currentInterview]);

        Assert.Equal(AgentRecruitingReadinessStatus.Ready, ready.Status);
        Assert.True(ready.ReadyForProduction);
        Assert.False(ready.ActivatesAgent);
        Assert.True(ready.RequiresSeparateActivationAuthorization);
        Assert.Equal(currentInterview.Id, ready.QualifyingInterviewId);
        Assert.Equal(currentAttempt.Id, ready.QualifyingAttemptId);
        Assert.Equal(qualifyingReview.Id, ready.QualifyingReviewId);
        Assert.Equal("change-control/CAB-42", ready.HumanAuthorizationReference);
        Assert.Equal(HashA, ready.HumanAuthorizationEvidenceHash);
    }

    [Fact]
    public void Project_distinguishes_incomplete_evidence_from_human_rejection()
    {
        var incomplete = CreateAttempt(
            sequence: 1,
            completeness: AgentRecruitingEvidenceCompleteness.Incomplete,
            automatedDecision: AgentRecruitingAutomatedDecision.Passed);
        var incompleteReadiness = AgentRecruitingReadinessProjector.Project(
            CandidateId,
            "current-version",
            [CreateInterview("current-version", [incomplete])]);

        var complete = CreateAttempt(
            sequence: 1,
            completeness: AgentRecruitingEvidenceCompleteness.Complete,
            automatedDecision: AgentRecruitingAutomatedDecision.Passed);
        var rejectedReadiness = AgentRecruitingReadinessProjector.Project(
            CandidateId,
            "current-version",
            [
                CreateInterview(
                    "current-version",
                    [complete],
                    [
                        CreateReview(
                            complete.Id,
                            AgentRecruitingHumanDecision.Rejected,
                            qualifiesForReadiness: false)
                    ])
            ]);

        Assert.Equal(
            AgentRecruitingReadinessStatus.IncompleteEvidence,
            incompleteReadiness.Status);
        Assert.Equal(
            AgentRecruitingReadinessStatus.Rejected,
            rejectedReadiness.Status);
        Assert.False(incompleteReadiness.ActivatesAgent);
        Assert.False(rejectedReadiness.ActivatesAgent);
    }

    [Fact]
    public void Project_preserves_repeated_attempt_comparison_and_latest_human_decision()
    {
        var first = CreateAttempt(
            sequence: 1,
            completeness: AgentRecruitingEvidenceCompleteness.Incomplete,
            automatedDecision: AgentRecruitingAutomatedDecision.Failed,
            score: 20m,
            createdAtUtc: Now);
        var second = CreateAttempt(
            sequence: 2,
            completeness: AgentRecruitingEvidenceCompleteness.Complete,
            automatedDecision: AgentRecruitingAutomatedDecision.Passed,
            score: 95m,
            createdAtUtc: Now.AddMinutes(1));
        var earlyRejection = CreateReview(
            second.Id,
            AgentRecruitingHumanDecision.Rejected,
            qualifiesForReadiness: false,
            reviewedAtUtc: Now.AddMinutes(2));
        var laterApproval = CreateReview(
            second.Id,
            AgentRecruitingHumanDecision.Approved,
            qualifiesForReadiness: true,
            reviewedAtUtc: Now.AddMinutes(3));
        var interview = CreateInterview(
            "current-version",
            [first, second],
            [laterApproval, earlyRejection]);

        var readiness = AgentRecruitingReadinessProjector.Project(
            CandidateId,
            "current-version",
            [interview]);

        Assert.Equal(AgentRecruitingReadinessStatus.Ready, readiness.Status);
        Assert.Collection(
            readiness.AttemptHistory,
            item =>
            {
                Assert.Equal(1, item.Sequence);
                Assert.Equal(AgentRecruitingEvidenceCompleteness.Incomplete, item.Completeness);
                Assert.Equal(AgentRecruitingAutomatedDecision.Failed, item.AutomatedDecision);
                Assert.Equal(20m, item.Score);
                Assert.Null(item.HumanDecision);
            },
            item =>
            {
                Assert.Equal(2, item.Sequence);
                Assert.Equal(AgentRecruitingEvidenceCompleteness.Complete, item.Completeness);
                Assert.Equal(AgentRecruitingAutomatedDecision.Passed, item.AutomatedDecision);
                Assert.Equal(95m, item.Score);
                Assert.Equal(AgentRecruitingHumanDecision.Approved, item.HumanDecision);
            });
    }

    [Fact]
    public void Project_later_rejected_recheck_revokes_earlier_qualifying_approval()
    {
        var initialAttempt = CreateAttempt(
            sequence: 1,
            completeness: AgentRecruitingEvidenceCompleteness.Complete,
            automatedDecision: AgentRecruitingAutomatedDecision.Passed,
            createdAtUtc: Now);
        var recheckAttempt = CreateAttempt(
            sequence: 2,
            completeness: AgentRecruitingEvidenceCompleteness.Complete,
            automatedDecision: AgentRecruitingAutomatedDecision.Passed,
            createdAtUtc: Now.AddMinutes(10));
        var interview = CreateInterview(
            "current-version",
            [initialAttempt, recheckAttempt],
            [
                CreateReview(
                    initialAttempt.Id,
                    AgentRecruitingHumanDecision.Approved,
                    qualifiesForReadiness: true,
                    reviewedAtUtc: Now.AddMinutes(1)),
                CreateReview(
                    recheckAttempt.Id,
                    AgentRecruitingHumanDecision.Rejected,
                    qualifiesForReadiness: false,
                    reviewedAtUtc: Now.AddMinutes(11))
            ]);

        var readiness = AgentRecruitingReadinessProjector.Project(
            CandidateId,
            "current-version",
            [interview]);

        Assert.Equal(AgentRecruitingReadinessStatus.Rejected, readiness.Status);
        Assert.False(readiness.ReadyForProduction);
        Assert.Null(readiness.QualifyingAttemptId);
        Assert.Null(readiness.QualifyingReviewId);
    }

    [Fact]
    public void Existing_json_without_optional_extension_fields_remains_readable()
    {
        var attempt = CreateAttempt(
            sequence: 1,
            completeness: AgentRecruitingEvidenceCompleteness.Incomplete,
            automatedDecision: AgentRecruitingAutomatedDecision.NeedsHumanReview);
        var source = CreateInterview("current-version", [attempt]);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(source, options);
        var restored = JsonSerializer.Deserialize<AgentRecruitingInterview>(json, options);

        Assert.DoesNotContain("\"analysis\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"recruitmentApplicationId\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"projectId\"", json, StringComparison.Ordinal);
        Assert.NotNull(restored);
        Assert.Null(restored.RecruitmentApplicationId);
        Assert.Null(restored.ProjectId);
        Assert.Null(Assert.Single(restored.Attempts).Analysis);
    }

    [Fact]
    public async Task Create_interview_requires_current_candidate_version_without_mutating_catalog()
    {
        var candidate = CreateAgent(CandidateId, AgentLifecycleStatus.Draft);
        var catalog = new RecordingCatalogStore(candidate, CreateAgent(EvaluatorId), CreateProvider());
        var store = new InMemoryEvidenceStore();
        var service = CreateService(catalog, store);
        var version = AgentConfigurationVersion.Create(candidate);

        var interview = await service.CreateInterviewAsync(
            new CreateAgentRecruitingInterviewCommand(
                CandidateId,
                version.ToLowerInvariant(),
                "  Production readiness interview  "));

        Assert.Equal(version, interview.CandidateConfigurationVersion);
        Assert.Equal("Production readiness interview", interview.Purpose);
        Assert.Equal(AgentLifecycleStatus.Draft, candidate.Status);
        Assert.Equal(0, catalog.SaveCount);

        var conflict = await Assert.ThrowsAsync<AgentRecruitingEvidenceException>(
            () => service.CreateInterviewAsync(
                new CreateAgentRecruitingInterviewCommand(
                    CandidateId,
                    "stale-version",
                    "Stale interview")));
        Assert.Equal(AgentRecruitingEvidenceFailureKind.Conflict, conflict.Kind);
        Assert.Equal("agent-recruiting.candidate-version-conflict", conflict.Code);
    }

    [Fact]
    public async Task Create_interview_preserves_recruitment_and_project_correlations()
    {
        var candidate = CreateAgent(CandidateId, AgentLifecycleStatus.Draft);
        var store = new InMemoryEvidenceStore();
        var service = CreateService(
            new RecordingCatalogStore(candidate, CreateProvider()),
            store);
        var recruitmentApplicationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var interview = await service.CreateInterviewAsync(
            new CreateAgentRecruitingInterviewCommand(
                CandidateId,
                AgentConfigurationVersion.Create(candidate),
                "Correlated assessment",
                recruitmentApplicationId,
                projectId));

        Assert.Equal(recruitmentApplicationId, interview.RecruitmentApplicationId);
        Assert.Equal(projectId, interview.ProjectId);

        var invalidCorrelation = await Assert.ThrowsAsync<AgentRecruitingEvidenceException>(
            () => service.CreateInterviewAsync(
                new CreateAgentRecruitingInterviewCommand(
                    CandidateId,
                    AgentConfigurationVersion.Create(candidate),
                    "Invalid correlation",
                    Guid.Empty)));
        Assert.Equal("agent-recruiting.identifier-invalid", invalidCorrelation.Code);
    }

    [Fact]
    public async Task List_candidate_interviews_filters_by_application_and_orders_newest_first()
    {
        var recruitmentApplicationId = Guid.NewGuid();
        var otherApplicationId = Guid.NewGuid();
        var oldest = CreateInterview("current-version") with
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000000"),
            CreatedAtUtc = Now.AddMinutes(-2),
            RecruitmentApplicationId = recruitmentApplicationId
        };
        var newest = CreateInterview("current-version") with
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000000"),
            CreatedAtUtc = Now,
            RecruitmentApplicationId = recruitmentApplicationId
        };
        var other = CreateInterview("current-version") with
        {
            Id = Guid.Parse("20000000-0000-0000-0000-000000000000"),
            CreatedAtUtc = Now.AddMinutes(-1),
            RecruitmentApplicationId = otherApplicationId
        };
        var service = CreateService(
            new RecordingCatalogStore(CreateAgent(CandidateId), CreateProvider()),
            new InMemoryEvidenceStore(oldest, other, newest));

        var all = await service.ListCandidateInterviewsAsync(CandidateId);
        var filtered = await service.ListCandidateInterviewsAsync(
            CandidateId,
            recruitmentApplicationId);

        Assert.Equal([newest.Id, other.Id, oldest.Id], all.Select(item => item.Id));
        Assert.Equal([newest.Id, oldest.Id], filtered.Select(item => item.Id));
    }

    [Fact]
    public async Task Readiness_is_scoped_to_the_selected_recruitment_application()
    {
        var candidate = CreateAgent(CandidateId);
        var currentVersion = AgentConfigurationVersion.Create(candidate);
        var approvedApplicationId = Guid.NewGuid();
        var pendingApplicationId = Guid.NewGuid();
        var attempt = CreateAttempt(
            sequence: 1,
            completeness: AgentRecruitingEvidenceCompleteness.Complete,
            automatedDecision: AgentRecruitingAutomatedDecision.Passed);
        var approvedInterview = CreateInterview(
            currentVersion,
            [attempt],
            [CreateReview(
                attempt.Id,
                AgentRecruitingHumanDecision.Approved,
                qualifiesForReadiness: true)]) with
        {
            RecruitmentApplicationId = approvedApplicationId
        };
        var pendingInterview = CreateInterview(currentVersion) with
        {
            RecruitmentApplicationId = pendingApplicationId
        };
        var service = CreateService(
            new RecordingCatalogStore(candidate, CreateProvider()),
            new InMemoryEvidenceStore(approvedInterview, pendingInterview));

        var approved = await service.GetCandidateReadinessAsync(
            CandidateId,
            approvedApplicationId);
        var pending = await service.GetCandidateReadinessAsync(
            CandidateId,
            pendingApplicationId);

        Assert.Equal(AgentRecruitingReadinessStatus.Ready, approved.Status);
        Assert.True(approved.ReadyForProduction);
        Assert.Equal(
            AgentRecruitingReadinessStatus.IncompleteEvidence,
            pending.Status);
        Assert.False(pending.ReadyForProduction);
        Assert.Empty(pending.AttemptHistory);
    }

    [Fact]
    public async Task Create_and_readiness_report_unknown_candidate()
    {
        var service = CreateService(
            new RecordingCatalogStore(CreateAgent(CandidateId), CreateProvider()),
            new InMemoryEvidenceStore());
        var unknown = Guid.NewGuid();

        var createFailure = await Assert.ThrowsAsync<AgentRecruitingEvidenceException>(
            () => service.CreateInterviewAsync(
                new CreateAgentRecruitingInterviewCommand(
                    unknown,
                    "version",
                    "Unknown candidate")));
        var readinessFailure = await Assert.ThrowsAsync<AgentRecruitingEvidenceException>(
            () => service.GetCandidateReadinessAsync(unknown));

        Assert.Equal("agent-recruiting.candidate-not-found", createFailure.Code);
        Assert.Equal("agent-recruiting.candidate-not-found", readinessFailure.Code);
    }

    [Theory]
    [InlineData(AgentRecruitingTargetKind.AgentExecutionRun)]
    [InlineData(AgentRecruitingTargetKind.WorkflowRun)]
    [InlineData(AgentRecruitingTargetKind.ProcessRun)]
    public async Task Append_attempt_accepts_each_supported_target_kind(
        AgentRecruitingTargetKind kind)
    {
        var candidate = CreateAgent(CandidateId);
        var store = new InMemoryEvidenceStore(
            CreateInterview(AgentConfigurationVersion.Create(candidate)));
        var targetId = Guid.NewGuid();
        var resolver = new DelegateTargetResolver(
            target => new AgentRecruitingTargetResolution(
                target.Id == targetId,
                "Completed",
                IsTerminal: true,
                [CandidateId]));
        var service = CreateService(
            new RecordingCatalogStore(candidate, CreateAgent(EvaluatorId), CreateProvider()),
            store,
            resolver);

        var updated = await service.AppendAttemptAsync(
            store.Interviews.Single().Id,
            CreateAttemptCommand(new AgentRecruitingExecutionTarget(kind, targetId)));

        var attempt = Assert.Single(updated.Attempts);
        Assert.Equal(kind, attempt.Target.Kind);
        Assert.Equal(targetId, attempt.Target.Id);
        Assert.Equal(kind, Assert.Single(resolver.ResolvedTargets).Kind);
    }

    [Fact]
    public async Task Append_attempt_rejects_missing_target_and_agent_candidate_mismatch()
    {
        var candidate = CreateAgent(CandidateId);
        var interview = CreateInterview(AgentConfigurationVersion.Create(candidate));
        var store = new InMemoryEvidenceStore(interview);
        var missingService = CreateService(
            new RecordingCatalogStore(candidate, CreateProvider()),
            store,
            new DelegateTargetResolver(
                _ => new AgentRecruitingTargetResolution(false, "not-found", false)));

        var notFound = await Assert.ThrowsAsync<AgentRecruitingEvidenceException>(
            () => missingService.AppendAttemptAsync(
                interview.Id,
                CreateAttemptCommand(
                    new AgentRecruitingExecutionTarget(
                        AgentRecruitingTargetKind.WorkflowRun,
                        Guid.NewGuid()))));

        var mismatchService = CreateService(
            new RecordingCatalogStore(candidate, CreateProvider()),
            store,
            new DelegateTargetResolver(
                _ => new AgentRecruitingTargetResolution(
                    true,
                    "Completed",
                    true,
                    [Guid.NewGuid()])));
        var mismatch = await Assert.ThrowsAsync<AgentRecruitingEvidenceException>(
            () => mismatchService.AppendAttemptAsync(
                interview.Id,
                CreateAttemptCommand(
                    new AgentRecruitingExecutionTarget(
                        AgentRecruitingTargetKind.AgentExecutionRun,
                        Guid.NewGuid()))));

        Assert.Equal(AgentRecruitingEvidenceFailureKind.NotFound, notFound.Kind);
        Assert.Equal("agent-recruiting.target-not-found", notFound.Code);
        Assert.Equal(AgentRecruitingEvidenceFailureKind.Conflict, mismatch.Kind);
        Assert.Equal("agent-recruiting.target-candidate-conflict", mismatch.Code);
        Assert.Empty(store.Interviews.Single().Attempts);
    }

    [Fact]
    public async Task Append_attempt_validates_hashes_and_rubric_and_records_incomplete_evidence()
    {
        var candidate = CreateAgent(CandidateId);
        var interview = CreateInterview(AgentConfigurationVersion.Create(candidate));
        var store = new InMemoryEvidenceStore(interview);
        var service = CreateService(
            new RecordingCatalogStore(candidate, CreateAgent(EvaluatorId), CreateProvider()),
            store,
            new DelegateTargetResolver(
                _ => new AgentRecruitingTargetResolution(
                    true,
                    "Running",
                    false,
                    [CandidateId])));
        var target = new AgentRecruitingExecutionTarget(
            AgentRecruitingTargetKind.WorkflowRun,
            Guid.NewGuid());

        var invalidHash = await Assert.ThrowsAsync<AgentRecruitingEvidenceException>(
            () => service.AppendAttemptAsync(
                interview.Id,
                CreateAttemptCommand(target) with { InputHash = "not-a-hash" }));
        var rubricConflict = await Assert.ThrowsAsync<AgentRecruitingEvidenceException>(
            () => service.AppendAttemptAsync(
                interview.Id,
                CreateAttemptCommand(target) with
                {
                    AutomatedEvaluation = CreateEvaluation() with
                    {
                        RubricVersion = "different-rubric"
                    }
                }));
        var incomplete = await service.AppendAttemptAsync(
            interview.Id,
            CreateAttemptCommand(target) with
            {
                InputHash = string.Empty,
                OutputHash = string.Empty,
                AutomatedEvaluation = CreateEvaluation() with
                {
                    EvaluatorAgentId = null,
                    ProviderProfileId = null,
                    Model = string.Empty,
                    EvaluatedAtUtc = default
                }
            });

        Assert.Equal("agent-recruiting.hash-invalid", invalidHash.Code);
        Assert.Equal("agent-recruiting.rubric-version-conflict", rubricConflict.Code);
        var attempt = Assert.Single(incomplete.Attempts);
        Assert.Equal(AgentRecruitingEvidenceCompleteness.Incomplete, attempt.Completeness);
        Assert.Contains("terminal-execution-target", attempt.MissingEvidence);
        Assert.Contains("input-hash", attempt.MissingEvidence);
        Assert.Contains("output-hash", attempt.MissingEvidence);
        Assert.Contains("evaluator-agent-id", attempt.MissingEvidence);
        Assert.Contains("evaluator-provider-profile-id", attempt.MissingEvidence);
        Assert.Contains("evaluator-model", attempt.MissingEvidence);
        Assert.Contains("evaluation-timestamp", attempt.MissingEvidence);
    }

    [Fact]
    public async Task Append_attempt_normalizes_strongly_typed_assessment_analysis()
    {
        var candidate = CreateAgent(CandidateId);
        var interview = CreateInterview(AgentConfigurationVersion.Create(candidate));
        var store = new InMemoryEvidenceStore(interview);
        var service = CreateService(
            new RecordingCatalogStore(candidate, CreateAgent(EvaluatorId), CreateProvider()),
            store);
        var target = new AgentRecruitingExecutionTarget(
            AgentRecruitingTargetKind.WorkflowRun,
            Guid.NewGuid());

        var updated = await service.AppendAttemptAsync(
            interview.Id,
            CreateAttemptCommand(target) with
            {
                Analysis = new AgentRecruitingAssessmentAnalysis(
                    AgentRecruitingAssessmentClassification.NeedsTraining,
                    0.82m,
                    "  Strong reasoning with a repeatable tool-selection gap.  ",
                    AgentRecruitingProposedNextStep.AssignTraining,
                    ["  Clear decomposition  "],
                    ["  Tool selection  "])
            });

        var analysis = Assert.Single(updated.Attempts).Analysis;
        Assert.NotNull(analysis);
        Assert.Equal(
            AgentRecruitingAssessmentClassification.NeedsTraining,
            analysis.Classification);
        Assert.Equal(0.82m, analysis.Confidence);
        Assert.Equal(
            "Strong reasoning with a repeatable tool-selection gap.",
            analysis.Summary);
        Assert.Equal(
            AgentRecruitingProposedNextStep.AssignTraining,
            analysis.ProposedNextStep);
        Assert.Equal(["Clear decomposition"], analysis.Strengths);
        Assert.Equal(["Tool selection"], analysis.Gaps);
    }

    [Fact]
    public async Task Append_attempt_rejects_invalid_assessment_analysis_without_appending()
    {
        var candidate = CreateAgent(CandidateId);
        var interview = CreateInterview(AgentConfigurationVersion.Create(candidate));
        var store = new InMemoryEvidenceStore(interview);
        var service = CreateService(
            new RecordingCatalogStore(candidate, CreateAgent(EvaluatorId), CreateProvider()),
            store);
        var target = new AgentRecruitingExecutionTarget(
            AgentRecruitingTargetKind.WorkflowRun,
            Guid.NewGuid());
        var valid = new AgentRecruitingAssessmentAnalysis(
            AgentRecruitingAssessmentClassification.Suitable,
            0.75m,
            "Suitable for supervised work.",
            AgentRecruitingProposedNextStep.RequestHumanReview,
            ["Deterministic output"],
            ["Needs broader evidence"]);

        var invalidClassification = await Assert.ThrowsAsync<AgentRecruitingEvidenceException>(
            () => service.AppendAttemptAsync(
                interview.Id,
                CreateAttemptCommand(target) with
                {
                    Analysis = valid with
                    {
                        Classification = (AgentRecruitingAssessmentClassification)0
                    }
                }));
        var invalidConfidence = await Assert.ThrowsAsync<AgentRecruitingEvidenceException>(
            () => service.AppendAttemptAsync(
                interview.Id,
                CreateAttemptCommand(target) with
                {
                    Analysis = valid with { Confidence = 1.01m }
                }));
        var invalidSummary = await Assert.ThrowsAsync<AgentRecruitingEvidenceException>(
            () => service.AppendAttemptAsync(
                interview.Id,
                CreateAttemptCommand(target) with
                {
                    Analysis = valid with { Summary = new string('x', 4001) }
                }));
        var invalidItems = await Assert.ThrowsAsync<AgentRecruitingEvidenceException>(
            () => service.AppendAttemptAsync(
                interview.Id,
                CreateAttemptCommand(target) with
                {
                    Analysis = valid with { Gaps = [" "] }
                }));

        Assert.Equal(
            "agent-recruiting.analysis-classification-invalid",
            invalidClassification.Code);
        Assert.Equal(
            "agent-recruiting.analysis-confidence-invalid",
            invalidConfidence.Code);
        Assert.Equal("agent-recruiting.text-invalid", invalidSummary.Code);
        Assert.Equal("agent-recruiting.analysis-items-invalid", invalidItems.Code);
        Assert.Empty(store.Interviews.Single().Attempts);
    }

    [Theory]
    [InlineData(AgentRecruitingTargetKind.AgentExecutionRun)]
    [InlineData(AgentRecruitingTargetKind.WorkflowRun)]
    [InlineData(AgentRecruitingTargetKind.ProcessRun)]
    public async Task Append_attempt_rejects_target_without_candidate_participation(
        AgentRecruitingTargetKind targetKind)
    {
        var candidate = CreateAgent(CandidateId);
        var interview = CreateInterview(AgentConfigurationVersion.Create(candidate));
        var store = new InMemoryEvidenceStore(interview);
        var service = CreateService(
            new RecordingCatalogStore(candidate, CreateProvider()),
            store,
            new DelegateTargetResolver(
                _ => new AgentRecruitingTargetResolution(
                    true,
                    "Completed",
                    true,
                    [Guid.NewGuid()])));

        var exception = await Assert.ThrowsAsync<AgentRecruitingEvidenceException>(
            () => service.AppendAttemptAsync(
                interview.Id,
                CreateAttemptCommand(
                    new AgentRecruitingExecutionTarget(
                        targetKind,
                        Guid.NewGuid()))));

        Assert.Equal(
            "agent-recruiting.target-candidate-conflict",
            exception.Code);
        Assert.Empty(store.Interviews.Single().Attempts);
    }

    [Fact]
    public async Task Human_approval_without_authorization_is_evidence_but_not_readiness()
    {
        var candidate = CreateAgent(CandidateId);
        var attempt = CreateAttempt(
            sequence: 1,
            completeness: AgentRecruitingEvidenceCompleteness.Complete,
            automatedDecision: AgentRecruitingAutomatedDecision.Passed);
        var interview = CreateInterview(
            AgentConfigurationVersion.Create(candidate),
            [attempt]);
        var store = new InMemoryEvidenceStore(interview);
        var service = CreateService(
            new RecordingCatalogStore(candidate, CreateProvider()),
            store);

        var reviewed = await service.AppendReviewAsync(
            interview.Id,
            new AppendAgentRecruitingReviewCommand(
                attempt.Id,
                AgentRecruitingHumanDecision.Approved,
                "reviewer-17",
                "Reviewer 17",
                string.Empty,
                string.Empty,
                "The technical evidence passed."));
        var readiness = await service.GetCandidateReadinessAsync(CandidateId);

        var review = Assert.Single(reviewed.Reviews);
        Assert.False(review.QualifiesForReadiness);
        Assert.Contains("human-authorization-reference", review.MissingEvidence);
        Assert.Contains("human-authorization-evidence-hash", review.MissingEvidence);
        Assert.Equal(
            AgentRecruitingReadinessStatus.AwaitingHumanApproval,
            readiness.Status);
        Assert.False(readiness.ReadyForProduction);
        Assert.False(readiness.ActivatesAgent);
    }

    private static AgentRecruitingEvidenceService CreateService(
        RecordingCatalogStore catalog,
        InMemoryEvidenceStore store,
        IAgentRecruitingTargetResolver? resolver = null)
        => new(
            catalog,
            store,
            resolver ?? new DelegateTargetResolver(
                _ => new AgentRecruitingTargetResolution(
                    true,
                    "Completed",
                    true,
                    [CandidateId])),
            new FixedTimeProvider(Now));

    private static AgentDefinition CreateAgent(
        Guid? id = null,
        AgentLifecycleStatus status = AgentLifecycleStatus.Active)
        => new(
            id ?? CandidateId,
            id == EvaluatorId ? "Recruiting evaluator" : "Candidate agent",
            "Evidence specialist",
            "Collects deterministic recruiting evidence.",
            "Evaluate evidence without mutating activation state.",
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
            Now,
            Now);

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
            Now,
            [],
            ProviderProfilePurpose.Chat);

    private static AgentRecruitingInterview CreateInterview(
        string configurationVersion,
        IReadOnlyList<AgentRecruitingAttempt>? attempts = null,
        IReadOnlyList<AgentRecruitingHumanReview>? reviews = null)
        => new(
            Guid.NewGuid(),
            CandidateId,
            configurationVersion,
            "Candidate agent",
            "gpt-test",
            "Production readiness",
            Now,
            attempts ?? [],
            reviews ?? []);

    private static AgentRecruitingAttempt CreateAttempt(
        int sequence,
        AgentRecruitingEvidenceCompleteness completeness,
        AgentRecruitingAutomatedDecision automatedDecision,
        decimal score = 90m,
        DateTimeOffset? createdAtUtc = null)
        => new(
            Guid.NewGuid(),
            Guid.Empty,
            sequence,
            new AgentRecruitingExecutionTarget(
                AgentRecruitingTargetKind.WorkflowRun,
                Guid.NewGuid()),
            "challenge",
            "v1",
            "rubric-v1",
            HashA,
            HashB,
            "contract-v1",
            HashA,
            "succeeded",
            CreateEvaluation(automatedDecision, score),
            completeness,
            completeness == AgentRecruitingEvidenceCompleteness.Complete
                ? []
                : ["missing-evidence"],
            createdAtUtc ?? Now);

    private static AgentRecruitingHumanReview CreateReview(
        Guid attemptId,
        AgentRecruitingHumanDecision decision,
        bool qualifiesForReadiness,
        DateTimeOffset? reviewedAtUtc = null)
        => new(
            Guid.NewGuid(),
            Guid.Empty,
            attemptId,
            decision,
            "reviewer-17",
            "Reviewer 17",
            qualifiesForReadiness ? "change-control/CAB-42" : string.Empty,
            qualifiesForReadiness ? HashA : string.Empty,
            "Independent human decision.",
            qualifiesForReadiness,
            qualifiesForReadiness
                ? []
                : ["human-authorization-reference", "human-authorization-evidence-hash"],
            reviewedAtUtc ?? Now.AddMinutes(1));

    private static AppendAgentRecruitingAttemptCommand CreateAttemptCommand(
        AgentRecruitingExecutionTarget target)
        => new(
            target,
            "challenge",
            "v1",
            "rubric-v1",
            HashA,
            HashB,
            "contract-v1",
            HashA,
            "succeeded",
            CreateEvaluation());

    private static AgentRecruitingAutomatedEvaluation CreateEvaluation(
        AgentRecruitingAutomatedDecision decision = AgentRecruitingAutomatedDecision.Passed,
        decimal score = 90m)
        => new(
            decision,
            score,
            EvaluatorId,
            ProviderId,
            "gpt-test",
            "rubric-v1",
            ["All assertions passed."],
            Now);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class DelegateTargetResolver(
        Func<AgentRecruitingExecutionTarget, AgentRecruitingTargetResolution> resolve)
        : IAgentRecruitingTargetResolver
    {
        public List<AgentRecruitingExecutionTarget> ResolvedTargets { get; } = [];

        public Task<AgentRecruitingTargetResolution> ResolveAsync(
            AgentRecruitingExecutionTarget target,
            CancellationToken cancellationToken = default)
        {
            ResolvedTargets.Add(target);
            return Task.FromResult(resolve(target));
        }
    }

    private sealed class RecordingCatalogStore : ISandboxWorkspaceCatalogStore
    {
        private SandboxWorkspaceCatalog catalog;

        public RecordingCatalogStore(params object[] items)
        {
            catalog = SandboxWorkspaceCatalog.Empty with
            {
                Agents = items.OfType<AgentDefinition>().ToList(),
                Providers = items.OfType<ProviderProfile>().ToList()
            };
        }

        public int SaveCount { get; private set; }

        public Task<SandboxWorkspaceCatalog> LoadCatalogAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(catalog);

        public Task<SandboxWorkspaceCatalogSnapshot> LoadCatalogSnapshotAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                new SandboxWorkspaceCatalogSnapshot(
                    catalog,
                    catalog.CatalogDataRevision));

        public Task<SandboxWorkspaceCatalog> SaveCatalogAsync(
            SandboxWorkspaceCatalog catalog,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            this.catalog = catalog;
            return Task.FromResult(catalog);
        }

        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
            Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            catalog = update(catalog);
            return Task.FromResult(catalog);
        }

        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
            Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            long expectedRevision,
            CancellationToken cancellationToken = default)
            => UpdateCatalogAsync(update, cancellationToken);
    }

    private sealed class InMemoryEvidenceStore(params AgentRecruitingInterview[] interviews)
        : IAgentRecruitingEvidenceStore
    {
        private readonly Dictionary<Guid, AgentRecruitingInterview> items =
            interviews.ToDictionary(item => item.Id);

        public IReadOnlyCollection<AgentRecruitingInterview> Interviews => items.Values;

        public Task<AgentRecruitingInterview> CreateInterviewAsync(
            AgentRecruitingInterview interview,
            CancellationToken cancellationToken = default)
        {
            items.Add(interview.Id, interview);
            return Task.FromResult(interview);
        }

        public Task<AgentRecruitingInterview?> GetInterviewAsync(
            Guid interviewId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(items.GetValueOrDefault(interviewId));

        public Task<IReadOnlyList<AgentRecruitingInterview>> ListCandidateInterviewsAsync(
            Guid candidateAgentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AgentRecruitingInterview>>(
                items.Values
                    .Where(item => item.CandidateAgentId == candidateAgentId)
                    .ToList());

        public Task<AgentRecruitingInterview> AppendAttemptAsync(
            Guid interviewId,
            AgentRecruitingAttempt attempt,
            CancellationToken cancellationToken = default)
        {
            var current = items[interviewId];
            var updated = current with
            {
                Attempts = [.. current.Attempts, attempt with { InterviewId = current.Id }]
            };
            items[interviewId] = updated;
            return Task.FromResult(updated);
        }

        public Task<AgentRecruitingInterview> AppendReviewAsync(
            Guid interviewId,
            AgentRecruitingHumanReview review,
            CancellationToken cancellationToken = default)
        {
            var current = items[interviewId];
            var updated = current with
            {
                Reviews = [.. current.Reviews, review with { InterviewId = current.Id }]
            };
            items[interviewId] = updated;
            return Task.FromResult(updated);
        }
    }
}
