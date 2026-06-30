using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class CognitiveMemoryReviewUiPlaywrightTests(PlaywrightAppFixture fixture)
{
    [Fact]
    public async Task CognitiveMemoryReviewUi_RendersReviewTraceAndHealthEvidence()
    {
        var seed = await SeedReviewUiEvidenceAsync();
        var artifactDirectory = Path.Combine(
            PlaywrightTestHostPaths.RepositoryRoot,
            ".artifacts",
            "playwright",
            "cognitive-memory-review-ui");
        Directory.CreateDirectory(artifactDirectory);

        var page = await fixture.Browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1440,
                Height = 1000
            }
        });
        try
        {
            var response = await page.GotoAsync($"{fixture.BaseUrl}/cognitive-memory?projectId={seed.ProjectId:D}");
            Assert.True(response?.Ok, $"Expected cognitive memory page to load. Logs:{Environment.NewLine}{fixture.GetLogSnapshot()}");

            await page.GetByTestId("cognitive-memory-summary").WaitForAsync();
            await WaitForLoadedDashboardAsync(page);
            await ExpectTextAsync(page, "Deployment rollback procedure");
            await Assertions.Expect(page.GetByTestId("cognitive-memory-projection-health")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("cognitive-memory-procedure-library")).ToBeVisibleAsync();
            await DismissDatabaseProfileDialogAsync(page);
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(artifactDirectory, "review-dashboard-desktop.png"),
                FullPage = true
            });

            await SelectTabAsync(page, "cognitive-memory-tab-memory", "cognitive-memory-explorer");
            await DismissDatabaseProfileDialogAsync(page);
            var memoryExplorer = page.GetByTestId("cognitive-memory-explorer");
            await memoryExplorer.WaitForAsync();
            await ExpectTextAsync(memoryExplorer, "Memory source evidence");
            await ExpectTextAsync(memoryExplorer, "Runtime source evidence.");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(artifactDirectory, "memory-explorer-desktop.png"),
                FullPage = true
            });

            await SelectTabAsync(page, "cognitive-memory-tab-review", "cognitive-memory-review-queue");
            await DismissDatabaseProfileDialogAsync(page);
            var reviewQueue = page.GetByTestId("cognitive-memory-review-queue");
            await reviewQueue.WaitForAsync();
            await ExpectTextAsync(reviewQueue, "Review items");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(artifactDirectory, "review-queue-desktop.png"),
                FullPage = true
            });

            await SelectTabAsync(page, "cognitive-memory-tab-traces", "cognitive-memory-trace-viewer");
            await DismissDatabaseProfileDialogAsync(page);
            var traceViewer = page.GetByTestId("cognitive-memory-trace-viewer");
            await traceViewer.WaitForAsync();
            await ExpectTextAsync(traceViewer, "Stage, candidate, and source evidence");
            await ExpectTextAsync(traceViewer, "Strong source-backed recall candidate.");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(artifactDirectory, "trace-viewer-desktop.png"),
                FullPage = true
            });

            await page.SetViewportSizeAsync(390, 900);
            await SelectTabAsync(page, "cognitive-memory-tab-health", "cognitive-memory-health");
            await DismissDatabaseProfileDialogAsync(page);
            var healthView = page.GetByTestId("cognitive-memory-health");
            await healthView.WaitForAsync();
            await ExpectTextAsync(healthView, "Fixture consolidation failure.");
            await ExpectTextAsync(healthView, "Projection is stale.");
            await ExpectTextAsync(healthView, "Procedure validation replay required.");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(artifactDirectory, "health-mobile.png"),
                FullPage = true
            });
        }
        catch
        {
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(artifactDirectory, "failure-state.png"),
                FullPage = true
            });
            throw;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private static async Task ExpectTextAsync(IPage page, string text)
        => await Assertions.Expect(page.GetByText(text, new PageGetByTextOptions { Exact = false }).First).ToBeVisibleAsync();

    private static async Task ExpectTextAsync(ILocator scope, string text)
        => await Assertions.Expect(scope.GetByText(text, new LocatorGetByTextOptions { Exact = false }).First).ToBeVisibleAsync();

    private static async Task WaitForLoadedDashboardAsync(IPage page)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(30);
        var dashboard = page.GetByTestId("cognitive-memory-dashboard-review");
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            await DismissDatabaseProfileDialogAsync(page);
            if (await IsVisibleAsync(dashboard, 500))
            {
                return;
            }

            await page.WaitForTimeoutAsync(250);
        }

        await dashboard.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 1_000
        });
    }

    private static async Task DismissDatabaseProfileDialogAsync(IPage page)
    {
        var heading = page.GetByText("Database profiles", new PageGetByTextOptions { Exact = true });
        if (!await IsVisibleAsync(heading, 500))
        {
            return;
        }

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions
        {
            Name = "Close",
            Exact = true
        }).ClickAsync();
        await heading.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = 5_000
        });
    }

    private static async Task SelectTabAsync(IPage page, string tabTestId, string panelTestId)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                var locator = page.GetByTestId(tabTestId);
                await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
                await locator.ClickAsync(new LocatorClickOptions { Force = true, Timeout = 5_000 });
                if (await IsVisibleAsync(page.GetByTestId(panelTestId), 1_000))
                {
                    return;
                }
            }
            catch (Exception exception) when (attempt < 7 && exception is PlaywrightException or TimeoutException)
            {
            }

            await page.WaitForTimeoutAsync(250);
        }

        await page.GetByTestId(panelTestId).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5_000
        });
    }

    private static async Task<bool> IsVisibleAsync(ILocator locator, float timeout)
    {
        try
        {
            await locator.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = timeout
            });
            return true;
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            return false;
        }
    }

    private async Task<SeedEvidence> SeedReviewUiEvidenceAsync()
    {
        await using var serviceProvider = BuildSeedServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var projectId = await CreateProjectAsync(projectsService, "Cognitive memory review UI proof");
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var now = DateTimeOffset.UnixEpoch;
        var maturityTraceId = Guid.NewGuid();
        var recallScoreTraceId = Guid.NewGuid();
        var simulationRiskTraceId = Guid.NewGuid();
        var replayPriorityTraceId = Guid.NewGuid();
        var consolidationRunId = Guid.NewGuid();
        var memoryRecord = new CognitiveMemoryRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Kind = CognitiveMemoryRecordKind.Procedural,
            Origin = CognitiveMemoryRecordOrigin.SourceDerived,
            Title = "Docker deploy memory",
            CanonicalText = "Docker deploy memory text.",
            SummaryText = "Docker deploy summary.",
            TopicKey = "docker.deploy",
            ValidationState = CognitiveMemoryValidationState.NeedsHumanReview,
            StabilityState = CognitiveMemoryStabilityState.Experimental,
            AlgorithmVersion = "playwright-test",
            ContentHash = CognitiveMemoryHash.FromUtf8("playwright component memory").Value,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var sourceManifest = new CognitiveMemorySourceManifestRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SourceSystem = "playwright-test",
            SourceScopeKey = "project:playwright",
            SourceSnapshotId = "playwright-snapshot",
            SnapshotHash = CognitiveMemoryHash.FromUtf8("playwright snapshot").Value,
            ProviderVersion = "playwright-test",
            ScanStatus = CognitiveMemoryRunStatus.Succeeded,
            ObservedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var sourceItem = new CognitiveMemorySourceItemRecord
        {
            Id = Guid.NewGuid(),
            SourceManifestId = sourceManifest.Id,
            ProjectId = projectId,
            SourceSystem = "playwright-test",
            SourceItemKey = "playwright-source-item",
            SourceItemType = "runbook",
            Title = "Playwright deployment runbook",
            ContentText = "Rollback source evidence.",
            Locator = "/playwright/source",
            ContentHash = CognitiveMemoryHash.FromUtf8("playwright source item").Value,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            AccessScope = projectId.ToString("D"),
            ObservedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var sourceLink = new CognitiveMemorySourceLinkRecord
        {
            Id = Guid.NewGuid(),
            MemoryRecordId = memoryRecord.Id,
            SourceManifestId = sourceManifest.Id,
            SourceItemId = sourceItem.Id,
            EvidenceRole = CognitiveMemoryEvidenceRole.PrimarySource,
            Locator = "/playwright/source",
            QuoteHash = CognitiveMemoryHash.FromUtf8("playwright quote").Value,
            Summary = "Runtime source evidence.",
            CreatedAtUtc = now
        };
        var evidenceAnchor = new CognitiveMemoryEvidenceAnchorRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SourceSystem = "playwright-test",
            Locator = "/playwright/source",
            StructuredPath = "$.source",
            QuoteHash = CognitiveMemoryHash.FromUtf8("playwright quote").Value,
            TrustLevel = CognitiveMemorySourceTrustLevel.RuntimeSource,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            SourceHash = CognitiveMemoryHash.FromUtf8("playwright source").Value,
            ObservedAtUtc = now,
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var procedureSkill = new CognitiveMemoryProcedureSkillRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Deployment rollback procedure",
            Purpose = "Rollback an unhealthy deployment.",
            Maturity = CognitiveMemoryProcedureSkillMaturity.Observed,
            RiskLevel = CognitiveMemoryRiskLevel.High,
            ValidationState = CognitiveMemoryValidationState.NeedsHumanReview,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            MaturityScoreEvaluationTraceId = maturityTraceId,
            MaturityBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            DisplayMaturityScore = 0.42,
            StepCount = 3,
            FailureModeCount = 1,
            ValidationEvidenceCount = 1,
            AlgorithmVersion = "playwright-test",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var reviewItem = new CognitiveMemoryReviewItemRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ReviewKind = CognitiveMemoryReviewKind.ProcedureSkill,
            Status = CognitiveMemoryReviewStatus.Pending,
            SubjectKind = CognitiveMemoryReviewSubjectKind.ProcedureSkill,
            SubjectId = procedureSkill.Id,
            RiskLevel = CognitiveMemoryRiskLevel.High,
            ReasonCode = "HighRiskProcedure",
            ReasonText = "High-risk procedure requires source-backed review.",
            SourceEvidenceCount = 1,
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var recallTrace = new CognitiveMemoryRecallTraceRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RecallMode = CognitiveMemoryRecallMode.FocusedTaskContext,
            RequestedByActorId = "agent:playwright",
            PolicyProfileId = "policy:playwright",
            RequestHash = CognitiveMemoryHash.FromUtf8("playwright recall").Value,
            AlgorithmVersion = "playwright-test",
            Outcome = CognitiveMemoryRunStatus.Succeeded,
            IncludedRecordCount = 1,
            ExcludedRecordCount = 1,
            SelectedClaimCount = 1,
            SelectedEvidenceAnchorCount = 1,
            InhibitedCandidateCount = 1,
            StartedAtUtc = now,
            CompletedAtUtc = now.AddMinutes(1),
            ConcurrencyToken = Guid.NewGuid()
        };
        var recallStage = new CognitiveMemoryRecallTraceStageRecord
        {
            Id = Guid.NewGuid(),
            RecallTraceId = recallTrace.Id,
            ProjectId = projectId,
            StageKind = CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
            ChannelKind = CognitiveMemoryRecallChannelKind.VectorProjection,
            Status = CognitiveMemoryRecallStageStatus.Completed,
            CandidateCount = 2,
            SelectedCount = 1,
            ExcludedCount = 1,
            StartedAtUtc = now,
            CompletedAtUtc = now.AddSeconds(10)
        };
        var recallCandidate = new CognitiveMemoryRecallCandidateRecord
        {
            Id = Guid.NewGuid(),
            RecallTraceId = recallTrace.Id,
            ProjectId = projectId,
            PrimaryChannelKind = CognitiveMemoryRecallChannelKind.VectorProjection,
            DecisionKind = CognitiveMemoryRecallCandidateDecisionKind.Selected,
            MemoryRecordId = memoryRecord.Id,
            EvidenceAnchorId = evidenceAnchor.Id,
            ScoreEvaluationTraceId = recallScoreTraceId,
            ScoreBucket = CognitiveMemoryScoreProjectionBucket.StrongAccept,
            DisplayRankProjection = 0.91,
            HasSourceDetail = true,
            SourceRefCount = 1,
            Title = "Docker deploy memory",
            Summary = "Selected due to source-backed deployment evidence.",
            Reason = "Strong source-backed recall candidate.",
            CreatedAtUtc = now
        };
        var sourceReference = new CognitiveMemoryRecallSourceRefRecord
        {
            Id = Guid.NewGuid(),
            RecallTraceId = recallTrace.Id,
            ProjectId = projectId,
            MemoryRecordId = memoryRecord.Id,
            EvidenceAnchorId = evidenceAnchor.Id,
            SourceSystem = "playwright-test",
            Locator = "/playwright/source",
            QuoteHash = evidenceAnchor.QuoteHash,
            Summary = "Runtime source evidence.",
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            IncludedInContext = true,
            CreatedAtUtc = now
        };
        var consolidationRun = new CognitiveMemoryConsolidationRunRecord
        {
            Id = consolidationRunId,
            ProjectId = projectId,
            Mode = CognitiveMemoryConsolidationMode.IncrementalRecent,
            TriggerKind = CognitiveMemoryConsolidationTriggerKind.SourceChanged,
            Status = CognitiveMemoryRunStatus.Failed,
            ProfileName = "playwright-test",
            IdempotencyKey = $"playwright-consolidation-{Guid.NewGuid():N}",
            InputHash = CognitiveMemoryHash.FromUtf8("playwright input").Value,
            OutputHash = CognitiveMemoryHash.FromUtf8("playwright output").Value,
            AlgorithmVersion = "playwright-test",
            SourceItemsScanned = 2,
            CandidatesCreated = 1,
            ReviewItemsCreated = 1,
            FailureCode = "FixtureFailure",
            FailureMessage = "Fixture consolidation failure.",
            StartedAtUtc = now,
            CompletedAtUtc = now.AddMinutes(2),
            ConcurrencyToken = Guid.NewGuid()
        };
        var projectionState = new CognitiveMemoryProjectionStateRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ProjectionKind = CognitiveMemoryProjectionKind.VectorCollection,
            TargetProvider = "qdrant",
            ProjectionSchemaVersion = "playwright-test",
            AlgorithmVersion = "playwright-test",
            Status = CognitiveMemoryProjectionStatus.RebuildRequired,
            LastSourceHash = CognitiveMemoryHash.FromUtf8("playwright projection").Value,
            FailureCode = "Stale",
            FailureMessage = "Projection is stale.",
            RebuildRequired = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var simulation = new CognitiveMemoryProcedureSimulationRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            OutputKind = CognitiveMemoryProcedureSimulationOutputKind.RiskAnalysis,
            Status = CognitiveMemoryProcedureSimulationStatus.NeedsReview,
            Summary = "Speculative rollback risk analysis.",
            IsSpeculative = true,
            SpeculationLabel = "speculative-hypothesis",
            RiskLevel = CognitiveMemoryRiskLevel.High,
            RiskScoreEvaluationTraceId = simulationRiskTraceId,
            RiskBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var replayJob = new CognitiveMemoryReplayJobRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            JobKind = CognitiveMemoryReplayJobKind.ValidateProcedure,
            State = CognitiveMemoryReplayJobState.NeedsReview,
            Reason = "Procedure validation replay required.",
            PriorityScoreEvaluationTraceId = replayPriorityTraceId,
            PriorityBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            DisplayPriorityProjection = 0.77,
            QueuePriority = 77,
            InputHash = CognitiveMemoryHash.FromUtf8("playwright replay").Value,
            ExpectedOutputSchema = "procedure-validation",
            AlgorithmVersion = "playwright-test",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };

        dbContext.AddRange(
            CreateScoreTrace(
                maturityTraceId,
                projectId,
                CognitiveMemoryScoreOwnerKind.ProcedureSkill,
                procedureSkill.Id,
                CognitiveMemoryScoreSpaceKind.ProcedureMaturity,
                CognitiveMemoryScoreProjectionBucket.NeedsReview,
                0.42,
                now),
            CreateScoreTrace(
                recallScoreTraceId,
                projectId,
                CognitiveMemoryScoreOwnerKind.RecallCandidate,
                recallCandidate.Id,
                CognitiveMemoryScoreSpaceKind.RecallCandidate,
                CognitiveMemoryScoreProjectionBucket.StrongAccept,
                0.91,
                now),
            CreateScoreTrace(
                simulationRiskTraceId,
                projectId,
                CognitiveMemoryScoreOwnerKind.ProcedureSimulation,
                simulation.Id,
                CognitiveMemoryScoreSpaceKind.SimulationRisk,
                CognitiveMemoryScoreProjectionBucket.NeedsReview,
                0.84,
                now),
            CreateScoreTrace(
                replayPriorityTraceId,
                projectId,
                CognitiveMemoryScoreOwnerKind.ReplayJob,
                replayJob.Id,
                CognitiveMemoryScoreSpaceKind.ReplayPriority,
                CognitiveMemoryScoreProjectionBucket.NeedsReview,
                0.77,
                now),
            new CognitiveMemoryRunRecord
            {
                Id = consolidationRunId,
                ProjectId = projectId,
                RunKind = CognitiveMemoryRunKind.Consolidation,
                Status = CognitiveMemoryRunStatus.Failed,
                OperationMode = CognitiveMemoryOperationMode.Observe,
                IdempotencyKey = consolidationRun.IdempotencyKey,
                InputHash = CognitiveMemoryHash.FromUtf8("playwright run input").Value,
                AlgorithmVersion = "playwright-test",
                FailureCode = "FixtureFailure",
                FailureMessage = "Fixture consolidation failure.",
                StartedAtUtc = now,
                CompletedAtUtc = now.AddMinutes(2),
                ConcurrencyToken = Guid.NewGuid()
            },
            sourceManifest,
            sourceItem,
            memoryRecord,
            sourceLink,
            evidenceAnchor,
            procedureSkill,
            reviewItem,
            recallTrace,
            recallStage,
            recallCandidate,
            sourceReference,
            consolidationRun,
            projectionState,
            simulation,
            replayJob);
        await dbContext.SaveChangesAsync();
        return new SeedEvidence(projectId, reviewItem.Id);
    }

    private ServiceProvider BuildSeedServiceProvider()
    {
        var activeProfile = CreateActiveProfile();
        var services = new ServiceCollection();
        var environment = new TestHostEnvironment(
            activeProfile.EnvironmentRootPath,
            "CanDoItAll.Tests.Playwright.CognitiveMemory");
        var configuration = TestApplicationBootstrap.BuildConfiguration(
            activeProfile,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            });

        TestApplicationBootstrap.ConfigureDefaultServices(services, configuration, environment);
        services.AddScoped<NavigationManager, SeedNavigationManager>();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private TestDatabaseProfile CreateActiveProfile()
    {
        if (string.IsNullOrWhiteSpace(fixture.DatabaseConnectionString))
        {
            throw new InvalidOperationException("Playwright fixture did not expose a database connection string.");
        }

        if (string.IsNullOrWhiteSpace(fixture.StorageWorkspaceRoot))
        {
            throw new InvalidOperationException("Playwright fixture did not expose the storage workspace root.");
        }

        var workspaceRoot = fixture.StorageWorkspaceRoot;
        var profileRoot = Directory.GetParent(workspaceRoot)?.FullName
            ?? throw new InvalidOperationException($"Could not resolve profile root from '{workspaceRoot}'.");
        var environmentRoot = Path.GetFullPath(Path.Combine(profileRoot, "..", ".."));

        return new TestDatabaseProfile(
            "playwright-cognitive-memory-seed",
            environmentRoot,
            profileRoot,
            TestDatabaseProviderKind.PostgreSql,
            fixture.DatabaseConnectionString,
            workspaceRoot,
            Path.Combine(profileRoot, "manager-artifacts"));
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Validation"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static CognitiveMemoryScoreEvaluationTraceRecord CreateScoreTrace(
        Guid id,
        Guid projectId,
        CognitiveMemoryScoreOwnerKind ownerKind,
        Guid ownerId,
        CognitiveMemoryScoreSpaceKind spaceKind,
        CognitiveMemoryScoreProjectionBucket bucket,
        double displayScore,
        DateTimeOffset now)
    {
        return new CognitiveMemoryScoreEvaluationTraceRecord
        {
            Id = id,
            ProjectId = projectId,
            OwnerKind = ownerKind,
            OwnerId = ownerId,
            SpaceKind = spaceKind,
            SchemaVersion = "playwright-test",
            NormalizationProfile = "playwright-test",
            AlgorithmVersion = "playwright-test",
            InputHash = CognitiveMemoryHash.FromUtf8($"{ownerKind}:{ownerId:D}:{spaceKind}").Value,
            ScalarProjectionKind = CognitiveMemoryScoreScalarProjectionKind.DisplayOnly,
            ProjectionBucket = bucket,
            DisplayScore = displayScore,
            MatchedShapeCount = 1,
            TracePayloadJson = "{}",
            CalculatedAtUtc = now,
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
    }

    private sealed class SeedNavigationManager : NavigationManager
    {
        public SeedNavigationManager()
        {
            Initialize("http://localhost/", "http://localhost/");
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
        }
    }

    private sealed record SeedEvidence(Guid ProjectId, Guid ReviewItemId);
}
