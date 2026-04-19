using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using ModelContextProtocol.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessesMcpStdioIntegrationTests
{
    private const string RepositoryRoot = @"C:\repositories\CanDoItAll";
    private static readonly string ServerAssemblyPath = Path.GetFullPath(Path.Combine(RepositoryRoot, @"src\CanDoItAll.Mcp.Processes\bin\Debug\net10.0\CanDoItAll.Mcp.Processes.dll"));

    [Fact]
    public async Task ProcessesMcp_stdio_call_lists_seeded_process_definitions()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var seedService = scope.ServiceProvider.GetRequiredService<ProcessDevelopmentSeedService>();
        var seedResult = await seedService.SeedBaselineAsync();

        Assert.True(seedResult.IsSuccess);
        Assert.NotNull(seedResult.Value);

        var settingsPath = Path.Combine(Path.GetTempPath(), $"candoitall-processes-mcp-{Guid.NewGuid():N}.json");
        try
        {
            var settings = new
            {
                Server = new
                {
                    Name = "CanDoItAll.Mcp.Processes",
                    RepositoryRoot = Path.GetFullPath(RepositoryRoot),
                    EnsureCurrentProfileReadyOnStartup = true
                },
                Database = new
                {
                    Provider = application.ActiveProfile.Provider switch
                    {
                        CanDoItAll.Tests.Support.TestDatabaseProviderKind.Sqlite => "Sqlite",
                        CanDoItAll.Tests.Support.TestDatabaseProviderKind.PostgreSql => "Postgres",
                        _ => "InMemory"
                    },
                    ConnectionString = application.ActiveProfile.ConnectionString
                },
                Storage = new
                {
                    WorkspaceRoot = application.ActiveProfile.WorkspaceRootPath,
                    ManagedFilesFolder = "managed-files",
                    ExportsFolder = "exports",
                    EvidenceFolder = "evidence",
                    ManagerArtifactsFolder = application.ActiveProfile.ManagerArtifactsRootPath
                },
                Workbench = new
                {
                    MaxWarmTabs = 3,
                    SleepAfterMinutes = 15,
                    BrowserStorageKey = "candoitall.workbench.session"
                },
                DevelopmentManager = new
                {
                    TuningModeEnabled = true,
                    ReviewBeforeSend = true,
                    ManagerBaseUrl = "http://127.0.0.1:6407"
                }
            };

            await File.WriteAllTextAsync(settingsPath, JsonSerializer.Serialize(settings));

            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "CanDoItAll.Tests.Integration",
                Command = "dotnet",
                Arguments =
                [
                    ServerAssemblyPath,
                    "--settings",
                    settingsPath
                ],
                WorkingDirectory = Path.GetFullPath(RepositoryRoot),
                ShutdownTimeout = TimeSpan.FromSeconds(15)
            });

            await using var client = await McpClient.CreateAsync(transport);
            var result = await client.CallToolAsync("processes_definitions_list", new Dictionary<string, object?>());

            Assert.False(result.IsError ?? false);
            Assert.True(result.StructuredContent is JsonElement { ValueKind: JsonValueKind.Object });
            var envelope = (JsonElement)result.StructuredContent!;
            Assert.True(envelope.GetProperty("ok").GetBoolean());
            Assert.Contains(
                envelope.GetProperty("data").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()),
                id => id == seedResult.Value.PrimaryDefinitionId);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Fact]
    public async Task ProcessesMcp_stdio_call_transitions_a_seeded_step_run()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var projectId = await CreateProjectAsync(projectsService, "Processes MCP stdio transition project");
        var definitionId = await CreatePublishedLinearDefinitionAsync(processesService, projectId);

        var settingsPath = Path.Combine(Path.GetTempPath(), $"candoitall-processes-mcp-{Guid.NewGuid():N}.json");
        try
        {
            var settings = new
            {
                Server = new
                {
                    Name = "CanDoItAll.Mcp.Processes",
                    RepositoryRoot = Path.GetFullPath(RepositoryRoot),
                    EnsureCurrentProfileReadyOnStartup = true
                },
                Database = new
                {
                    Provider = application.ActiveProfile.Provider switch
                    {
                        CanDoItAll.Tests.Support.TestDatabaseProviderKind.Sqlite => "Sqlite",
                        CanDoItAll.Tests.Support.TestDatabaseProviderKind.PostgreSql => "Postgres",
                        _ => "InMemory"
                    },
                    ConnectionString = application.ActiveProfile.ConnectionString
                },
                Storage = new
                {
                    WorkspaceRoot = application.ActiveProfile.WorkspaceRootPath,
                    ManagedFilesFolder = "managed-files",
                    ExportsFolder = "exports",
                    EvidenceFolder = "evidence",
                    ManagerArtifactsFolder = application.ActiveProfile.ManagerArtifactsRootPath
                },
                Workbench = new
                {
                    MaxWarmTabs = 3,
                    SleepAfterMinutes = 15,
                    BrowserStorageKey = "candoitall.workbench.session"
                },
                DevelopmentManager = new
                {
                    TuningModeEnabled = true,
                    ReviewBeforeSend = true,
                    ManagerBaseUrl = "http://127.0.0.1:6407"
                }
            };

            await File.WriteAllTextAsync(settingsPath, JsonSerializer.Serialize(settings));

            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "CanDoItAll.Tests.Integration",
                Command = "dotnet",
                Arguments =
                [
                    ServerAssemblyPath,
                    "--settings",
                    settingsPath
                ],
                WorkingDirectory = Path.GetFullPath(RepositoryRoot),
                ShutdownTimeout = TimeSpan.FromSeconds(15)
            });

            await using var client = await McpClient.CreateAsync(transport);
            var startRunResult = await client.CallToolAsync(
                "processes_run_start",
                new Dictionary<string, object?>
                {
                    ["request"] = new Dictionary<string, object?>
                    {
                        ["processDefinitionId"] = definitionId,
                        ["projectId"] = projectId,
                        ["runName"] = "Processes MCP stdio transition run",
                        ["operatingMode"] = nameof(ProcessOperatingMode.AssistedExecution),
                        ["triggerReason"] = "Integration verification over stdio."
                    }
                });

            Assert.False(startRunResult.IsError ?? false);
            var startRunEnvelope = (JsonElement)startRunResult.StructuredContent!;
            Assert.True(
                startRunEnvelope.GetProperty("ok").GetBoolean(),
                startRunEnvelope.TryGetProperty("error", out var startRunError)
                    ? startRunError.ToString()
                    : "The run start tool returned a failed envelope.");

            var runId = startRunEnvelope.GetProperty("data").GetGuid();

            var runDetailResult = await client.CallToolAsync(
                "processes_run_detail_get",
                new Dictionary<string, object?>
                {
                    ["runId"] = runId
                });

            Assert.False(runDetailResult.IsError ?? false);
            var runDetailEnvelope = (JsonElement)runDetailResult.StructuredContent!;
            Assert.True(runDetailEnvelope.GetProperty("ok").GetBoolean());

            var firstStepRun = runDetailEnvelope.GetProperty("data")
                .GetProperty("stepRuns")
                .EnumerateArray()
                .OrderBy(item => item.GetProperty("sequence").GetInt32())
                .First();
            Assert.Equal(nameof(ProcessStepRunStatus.Ready), firstStepRun.GetProperty("status").GetString());

            var transitionResult = await client.CallToolAsync(
                "processes_step_transition",
                new Dictionary<string, object?>
                {
                    ["request"] = new Dictionary<string, object?>
                    {
                        ["stepRunId"] = firstStepRun.GetProperty("id").GetGuid(),
                        ["targetStatus"] = nameof(ProcessStepRunStatus.InProgress),
                        ["reason"] = "stdio regression proof",
                        ["decidedBy"] = "integration-test"
                    }
                });

            Assert.False(transitionResult.IsError ?? false);
            var transitionEnvelope = (JsonElement)transitionResult.StructuredContent!;
            Assert.True(
                transitionEnvelope.GetProperty("ok").GetBoolean(),
                transitionEnvelope.TryGetProperty("error", out var transitionError)
                    ? transitionError.ToString()
                    : "The step transition tool returned a failed envelope.");

            var updatedRunDetailResult = await client.CallToolAsync(
                "processes_run_detail_get",
                new Dictionary<string, object?>
                {
                    ["runId"] = runId
                });

            Assert.False(updatedRunDetailResult.IsError ?? false);
            var updatedRunDetailEnvelope = (JsonElement)updatedRunDetailResult.StructuredContent!;
            var updatedFirstStepRun = updatedRunDetailEnvelope.GetProperty("data")
                .GetProperty("stepRuns")
                .EnumerateArray()
                .OrderBy(item => item.GetProperty("sequence").GetInt32())
                .First();

            Assert.Equal(nameof(ProcessStepRunStatus.InProgress), updatedFirstStepRun.GetProperty("status").GetString());
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Fact]
    public async Task ProcessesMcp_stdio_run_detail_keeps_improvements_scoped_to_the_selected_run()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = await CreateProjectAsync(projectsService, "Processes MCP stdio improvement scope project");
        var definitionId = await CreatePublishedLinearDefinitionAsync(processesService, projectId);
        var firstRunId = await StartRunAsync(processesService, definitionId, projectId, "Processes MCP first run");
        var secondRunId = await StartRunAsync(processesService, definitionId, projectId, "Processes MCP second run");

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<ProcessImprovementCandidate>().Add(new ProcessImprovementCandidate
            {
                ProcessDefinitionId = definitionId,
                ProcessRunId = firstRunId,
                Title = "Repair the first run",
                Category = nameof(ProcessStepRunStatus.Failed),
                ProblemSummary = "The first run exposed a process defect.",
                EvidenceSummary = "Only the first run should surface this improvement.",
                Status = ProcessImprovementStatus.Open,
                IsTrainingOpportunity = false,
                RequiresGovernanceReview = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var settingsPath = Path.Combine(Path.GetTempPath(), $"candoitall-processes-mcp-{Guid.NewGuid():N}.json");
        try
        {
            var settings = new
            {
                Server = new
                {
                    Name = "CanDoItAll.Mcp.Processes",
                    RepositoryRoot = Path.GetFullPath(RepositoryRoot),
                    EnsureCurrentProfileReadyOnStartup = true
                },
                Database = new
                {
                    Provider = application.ActiveProfile.Provider switch
                    {
                        CanDoItAll.Tests.Support.TestDatabaseProviderKind.Sqlite => "Sqlite",
                        CanDoItAll.Tests.Support.TestDatabaseProviderKind.PostgreSql => "Postgres",
                        _ => "InMemory"
                    },
                    ConnectionString = application.ActiveProfile.ConnectionString
                },
                Storage = new
                {
                    WorkspaceRoot = application.ActiveProfile.WorkspaceRootPath,
                    ManagedFilesFolder = "managed-files",
                    ExportsFolder = "exports",
                    EvidenceFolder = "evidence",
                    ManagerArtifactsFolder = application.ActiveProfile.ManagerArtifactsRootPath
                },
                Workbench = new
                {
                    MaxWarmTabs = 3,
                    SleepAfterMinutes = 15,
                    BrowserStorageKey = "candoitall.workbench.session"
                },
                DevelopmentManager = new
                {
                    TuningModeEnabled = true,
                    ReviewBeforeSend = true,
                    ManagerBaseUrl = "http://127.0.0.1:6407"
                }
            };

            await File.WriteAllTextAsync(settingsPath, JsonSerializer.Serialize(settings));

            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "CanDoItAll.Tests.Integration",
                Command = "dotnet",
                Arguments =
                [
                    ServerAssemblyPath,
                    "--settings",
                    settingsPath
                ],
                WorkingDirectory = Path.GetFullPath(RepositoryRoot),
                ShutdownTimeout = TimeSpan.FromSeconds(15)
            });

            await using var client = await McpClient.CreateAsync(transport);

            var firstRunDetailResult = await client.CallToolAsync(
                "processes_run_detail_get",
                new Dictionary<string, object?>
                {
                    ["runId"] = firstRunId
                });
            Assert.False(firstRunDetailResult.IsError ?? false);
            var firstRunDetailEnvelope = (JsonElement)firstRunDetailResult.StructuredContent!;
            Assert.True(firstRunDetailEnvelope.GetProperty("ok").GetBoolean());
            Assert.Single(firstRunDetailEnvelope.GetProperty("data").GetProperty("improvements").EnumerateArray());

            var secondRunDetailResult = await client.CallToolAsync(
                "processes_run_detail_get",
                new Dictionary<string, object?>
                {
                    ["runId"] = secondRunId
                });
            Assert.False(secondRunDetailResult.IsError ?? false);
            var secondRunDetailEnvelope = (JsonElement)secondRunDetailResult.StructuredContent!;
            Assert.True(secondRunDetailEnvelope.GetProperty("ok").GetBoolean());
            Assert.Empty(secondRunDetailEnvelope.GetProperty("data").GetProperty("improvements").EnumerateArray());
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Execution"
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task<Guid> CreatePublishedLinearDefinitionAsync(ProcessesService processesService, Guid projectId)
    {
        var definitionId = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(definitionId.IsSuccess, string.Join(" | ", definitionId.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(definitionId.Value);

        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));
        return definitionId.Value;
    }

    private static async Task<Guid> StartRunAsync(ProcessesService processesService, Guid definitionId, Guid projectId, string runName)
    {
        var startResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = definitionId,
            ProjectId = projectId,
            RunName = runName,
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration verification"
        });

        Assert.True(startResult.IsSuccess, string.Join(" | ", startResult.Errors.Select(error => error.Message)));
        return startResult.Value;
    }

    private static ProcessDefinitionEditorModel BuildDefinitionEditor(Guid projectId, Guid managerRoleId)
    {
        var intakeStepId = Guid.NewGuid();
        var deliveryReadinessArtifactId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Integration delivery process",
            Summary = "Validates role-first process runtime behavior.",
            ValueStatement = "Keep definition, runtime, and governance evidence on one durable model.",
            CustomerName = "Acme Customer",
            OwnerName = "Morgan Process Lead",
            GovernancePolicySummary = "Delivery work requires recorded runtime evidence.",
            ChangeSummary = "Initial integration definition.",
            ConstitutionRuleSummary = "Role contracts outlive executor changes.",
            OperatingModeSummary = "Assisted execution with explicit review.",
            SimulationReadinessSummary = "Safe for local integration validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = managerRoleId,
                    Key = "delivery-owner",
                    DisplayName = "Delivery owner",
                    Purpose = "Own the delivery readiness path.",
                    StaffingIntent = "Primary delivery-side owner for the project.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Delivery owner snapshot."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "intake",
                    Title = "Capture intake",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Project scope and delivery notes.",
                    OutputContractSummary = "Typed intake package.",
                    EvidenceContractSummary = "Intake evidence retained for review.",
                    DecisionRightsSummary = "Delivery owner can move intake forward.",
                    ExceptionPolicySummary = "Escalate missing scope details.",
                    TargetLeadHours = 2,
                    CanvasX = 140,
                    CanvasY = 160,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            RebindPolicySummary = "Rebind to the current delivery owner."
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Key = "delivery-review",
                    Title = "Review delivery readiness",
                    StepKind = ProcessStepKind.Work,
                    InputContractSummary = "Typed intake package.",
                    OutputContractSummary = "Delivery readiness conclusion.",
                    EvidenceContractSummary = "Blocked reasons or readiness proof.",
                    DecisionRightsSummary = "Delivery owner decides whether to proceed or block.",
                    ExceptionPolicySummary = "Block when evidence is missing.",
                    TargetLeadHours = 4,
                    Dependencies = CreateDependencies((intakeStepId, null)),
                    CanvasX = 420,
                    CanvasY = 160,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            RebindPolicySummary = "Delivery owner remains explicitly assigned."
                        }
                    ],
                    ArtifactExpectations =
                    [
                        new ProcessArtifactExpectationEditorModel
                        {
                            Id = deliveryReadinessArtifactId,
                            ArtifactKind = ProcessArtifactKind.Evidence,
                            Title = "Delivery readiness evidence",
                            ValidationRequirementSummary = "Human review required before final approval."
                        }
                    ]
                }
            ]
        };
    }

    private static List<ProcessStepDependencyEditorModel> CreateDependencies(params (Guid StepId, Guid? BranchOutcomeId)[] items)
    {
        return items
            .Select(item => new ProcessStepDependencyEditorModel
            {
                Id = Guid.NewGuid(),
                DependsOnStepId = item.StepId,
                DependsOnBranchOutcomeId = item.BranchOutcomeId
            })
            .ToList();
    }
}
