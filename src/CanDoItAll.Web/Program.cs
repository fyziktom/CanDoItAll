using CanDoItAll.Components;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Components.Mermaid.Infrastructure;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Readiness;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Activity;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Validation;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Components;
using CanDoItAll.Web.Composition;
using CanDoItAll.Web.Infrastructure;
using CanDoItAll.Web;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
var detailedErrorsEnabled = builder.Configuration.GetValue<bool?>("DetailedErrors") ?? builder.Environment.IsDevelopment();
var promptAttachmentMessageLimitBytes = 8 * 1024 * 1024;

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = detailedErrorsEnabled)
    // Prompt-session attachments are posted through JS interop, so the default 32 KB SignalR limit
    // is too small for screenshots and other evidence files added from the canvas wizard.
    .AddHubOptions(options => options.MaximumReceiveMessageSize = promptAttachmentMessageLimitBytes);

builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddCanDoItAllCharts();
builder.Services.AddCanDoItAllInfrastructure(builder.Configuration, builder.Environment, CanDoItAll.Web.Composition.ModuleAssemblies.All);
builder.Services.AddCanDoItAllRuntimeDatabaseSwitching();
builder.Services.AddCanDoItAllRuntimeModules(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddCanDoItAllApi(builder.Configuration);
builder.Services.AddCanDoItAllMermaid();
builder.Services.AddHttpClient<DevelopmentManagerClient>();
builder.Services.AddScoped<IWorkbenchStateStore, BrowserWorkspaceStateStore>();
builder.Services.AddScoped<TuningCoordinator>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
var apiOptions = app.Services.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
if (apiOptions.Authorization.Enabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseAntiforgery();
app.MapStaticAssets();
app.MapCanDoItAllManagedFiles();

if (apiOptions.OpenApiEnabled)
{
    var openApiEndpoint = app.MapOpenApi();
    var swaggerJsonEndpoint = app.MapOpenApi("/swagger/{documentName}/swagger.json");
    if (apiOptions.Authorization.Enabled)
    {
        openApiEndpoint.RequireAuthorization();
        swaggerJsonEndpoint.RequireAuthorization();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapGet("/_dev/runtime", (IRuntimeReadinessService readiness) =>
    {
        var iteration = int.TryParse(Environment.GetEnvironmentVariable("DOTNET_WATCH_ITERATION"), out var parsed)
            ? parsed
            : (int?)null;

        var snapshot = readiness.GetSnapshot();

        return Results.Ok(new
        {
            snapshot.IsReady,
            snapshot.EnvironmentName,
            snapshot.Summary,
            WatchIteration = iteration,
            HotReloadGeneration = RuntimeHotReloadTracker.CurrentGeneration,
            RuntimePid = Environment.ProcessId,
            OwnerKind = app.Configuration["CanDoItAllMcpOwnerKind"],
            OwnerId = app.Configuration["CanDoItAllMcpOwnerId"],
            ServerInstanceId = app.Configuration["CanDoItAllMcpServerInstanceId"],
            snapshot.StartedAtUtc,
            snapshot.LastChangedAtUtc,
            snapshot.ActiveUrls
        });
    });

    app.MapGet("/_dev/database/selection", (IDatabaseProfileRuntimeAccessor profileAccessor) =>
    {
        var profile = profileAccessor.ResolveCurrentProfile();
        return Results.Ok(new
        {
            profile.Profile.Id,
            profile.Profile.DisplayName,
            profile.Profile.ProviderKind,
            profile.Profile.SourceKind,
            profile.Profile.Runtime.Fingerprint,
            profile.Profile.Storage.WorkspaceRoot,
            profile.ConnectionString
        });
    });

    app.MapPost("/_dev/database/profiles/postgresql", async (
        PostgreSqlDevDatabaseProfileRequest request,
        IDatabaseProfileService profileService,
        IDatabaseProfileRuntimeAccessor profileAccessor,
        IDatabaseDriverRegistry driverRegistry,
        IAppDatabaseBootstrapper bootstrapper,
        IDatabaseSwitchCoordinator switchCoordinator) =>
    {
        var databaseName = request.DatabaseName?.Trim();
        var username = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            return Results.BadRequest(new[] { "PostgreSQL database name is required." });
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            return Results.BadRequest(new[] { "PostgreSQL username is required." });
        }

        var saveResult = await profileService.SaveAsync(new CanDoItAll.Infrastructure.ControlPlane.DatabaseProfileEditorModel
        {
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? $"PostgreSQL {databaseName}"
                : request.DisplayName.Trim(),
            ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection,
            WorkspaceRoot = request.WorkspaceRoot,
            PostgresHost = string.IsNullOrWhiteSpace(request.Host) ? "127.0.0.1" : request.Host.Trim(),
            PostgresPort = request.Port is > 0 ? request.Port.Value : 5432,
            PostgresDatabaseName = databaseName,
            PostgresUsername = username,
            PostgresPassword = request.Password ?? string.Empty,
            PostgresAdminDatabaseName = string.IsNullOrWhiteSpace(request.AdminDatabaseName)
                ? "postgres"
                : request.AdminDatabaseName.Trim(),
            PostgresTrustServerCertificate = request.TrustServerCertificate ?? false
        });
        if (saveResult.IsFailure)
        {
            return Results.BadRequest(saveResult.Errors.Select(error => error.Message).ToArray());
        }

        var profile = profileAccessor.ResolveProfile(saveResult.Value);
        await driverRegistry.Resolve(profile.Profile.ProviderKind).CreateEmptyAsync(profile);
        await bootstrapper.EnsureProfileReadyAsync(profile);

        object? switchResult = null;
        if (request.Activate != false)
        {
            var activation = await switchCoordinator.SwitchAsync(profile.Profile.Id);
            if (activation.IsFailure)
            {
                return Results.BadRequest(activation.Errors.Select(error => error.Message).ToArray());
            }

            switchResult = new
            {
                activation.Value!.Generation,
                activation.Value.CurrentProfileId
            };
        }

        return Results.Ok(new
        {
            profile.Profile.Id,
            profile.Profile.DisplayName,
            profile.Profile.ProviderKind,
            profile.Profile.SourceKind,
            profile.Profile.Runtime.Fingerprint,
            profile.Profile.Storage.WorkspaceRoot,
            Descriptor = $"{profile.Profile.PostgreSql?.Host}:{profile.Profile.PostgreSql?.Port}/{profile.Profile.PostgreSql?.DatabaseName}",
            Switch = switchResult
        });
    });

    app.MapPost("/_dev/database/switch/{profileId:guid}", async (
        Guid profileId,
        IDatabaseSwitchCoordinator switchCoordinator,
        IDatabaseProfileRuntimeAccessor profileAccessor) =>
    {
        var switchResult = await switchCoordinator.SwitchAsync(profileId);
        if (switchResult.IsFailure)
        {
            return Results.BadRequest(switchResult.Errors.Select(error => error.Message).ToArray());
        }

        var profile = profileAccessor.ResolveCurrentProfile();
        return Results.Ok(new
        {
            switchResult.Value!.Generation,
            switchResult.Value.CurrentProfileId,
            profile.Profile.DisplayName,
            profile.Profile.Runtime.Fingerprint,
            profile.Profile.Storage.WorkspaceRoot,
            profile.ConnectionString
        });
    });

    app.MapPost("/_dev/database/seed-profile", async (
        string? label,
        ProjectsService projectsService,
        IManagedArtifactStore managedArtifactStore) =>
    {
        var seedLabel = string.IsNullOrWhiteSpace(label)
            ? $"Seed {Guid.NewGuid():N}"[..12]
            : label.Trim();
        var saveResult = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = $"{seedLabel} Project",
            Description = $"{seedLabel} description",
            Objective = $"{seedLabel} objective",
            CurrentPhase = "Execution"
        });
        if (saveResult.IsFailure)
        {
            return Results.BadRequest(saveResult.Errors.Select(error => error.Message).ToArray());
        }

        var fileName = string.Concat(seedLabel.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "seed";
        }

        var relativePath = managedArtifactStore.GetRelativePath("profile-seeds", $"{fileName}.txt");
        var content = $"seed:{seedLabel}";
        var fullPath = await managedArtifactStore.SaveTextAsync("profile-seeds", $"{fileName}.txt", content);

        return Results.Ok(new
        {
            saveResult.Value,
            ProjectName = $"{seedLabel} Project",
            ManagedFileRelativePath = relativePath,
            ManagedFileFullPath = fullPath,
            ManagedFileContent = content
        });
    });

    app.MapPost("/_dev/projects", async (
        string? name,
        string? phase,
        ProjectsService projectsService) =>
    {
        var saveResult = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Runtime Switch {Guid.NewGuid():N}"[..24] : name.Trim(),
            Description = "Development-only runtime switch proof project.",
            Objective = "Drive stale-route recovery proof.",
            CurrentPhase = string.IsNullOrWhiteSpace(phase) ? "Execution" : phase.Trim()
        });
        if (saveResult.IsFailure)
        {
            return Results.BadRequest(saveResult.Errors.Select(error => error.Message).ToArray());
        }

        return Results.Ok(new
        {
            ProjectId = saveResult.Value,
            Route = $"/projects/{saveResult.Value:D}/structure"
        });
    });

    app.MapGet("/_dev/agentframework/diagnostics", async (
        AiAgentService aiAgentService,
        IAiTechnicalAgentBridge technicalAgentBridge,
        ICanDoItAllAgentWorkspaceFactory workspaceFactory,
        IDbContextFactory<AppDbContext> dbContextFactory) =>
    {
        await technicalAgentBridge.SynchronizeDirectoryProjectionAsync();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var parties = await dbContext.Set<Party>()
            .Where(item => item.PartyType == PartyType.AiAgent)
            .OrderBy(item => item.DisplayName)
            .Select(item => new
            {
                item.Id,
                item.DisplayName
            })
            .ToListAsync();
        var partyIds = parties.Select(item => item.Id).ToList();
        var bindings = await dbContext.Set<AiResourceBinding>()
            .Where(item => partyIds.Contains(item.PartyId))
            .Select(item => new
            {
                item.PartyId,
                item.TechnicalAgentId,
                item.BindingStatus,
                item.BindingReason
            })
            .ToListAsync();
        var summaries = await technicalAgentBridge.GetDirectorySummariesAsync(partyIds);
        var roster = await aiAgentService.ListAgentDirectoryAsync();
        var workspaceAgents = await workspaceFactory.GetOrganizationWorkspaceService().ListAgentsAsync(includeTemplates: false);

        return Results.Ok(new
        {
            PartyCount = parties.Count,
            BindingCount = bindings.Count,
            WorkspaceAgentCount = workspaceAgents.Count,
            RosterCount = roster.Count,
            Parties = parties.Select(item => new
            {
                item.Id,
                item.DisplayName,
                Summary = summaries.TryGetValue(item.Id, out var summary)
                    ? new
                    {
                        summary.TechnicalAgentId,
                        summary.BindingStatus,
                        summary.HasTechnicalProfile,
                        summary.ProviderName,
                        summary.DefaultModel,
                        summary.CapabilityCount,
                        summary.BindingSummary,
                        summary.AgentsRoute
                    }
                    : null
            }),
            Bindings = bindings,
            WorkspaceAgents = workspaceAgents.Select(item => new
            {
                item.Id,
                item.Name,
                item.Status,
                item.TemplateKey,
                item.IsTemplate,
                item.ProviderProfileId,
                item.ConfigurationJson,
                item.Tags
            }),
            Roster = roster.Select(item => new
            {
                item.PartyId,
                item.DisplayName,
                item.TechnicalAgentId,
                item.BindingStatus,
                item.HasProfile,
                item.ProviderName,
                item.DefaultModel,
                item.CapabilityCount
            })
        });
    });

    app.MapGet("/_dev/agentframework/credential", async (
        IConfiguration configuration,
        IAgentProviderCredentialResolver providerCredentialResolver,
        ICanDoItAllAgentWorkspaceFactory workspaceFactory) =>
    {
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var providers = await workspaceService.ListProvidersAsync();
        var provider = providers
            .FirstOrDefault(item => string.Equals(item.Name, ManagedSeedProviderFallbacks.OpenAiDefaultProviderName, StringComparison.OrdinalIgnoreCase))
            ?? providers.FirstOrDefault(item => item.Kind is CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi or CanDoItAll.AgentFramework.Models.ProviderKind.AzureOpenAi);

        if (provider is null)
        {
            return Results.NotFound(new
            {
                Error = "No OpenAI provider profile was found in the active workspace."
            });
        }

        var resolution = providerCredentialResolver.Resolve(provider);
        var processValue = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var configuredValue = configuration["OPENAI_API_KEY"];

        return Results.Ok(new
        {
            Provider = new
            {
                provider.Id,
                provider.Name,
                provider.Kind,
                provider.ApiKeyEnvironmentVariable,
                provider.DefaultModel,
                provider.Transport
            },
            ResolverType = providerCredentialResolver.GetType().FullName,
            Resolver = new
            {
                resolution.IsResolved,
                resolution.ResolutionSource,
                resolution.FailureMessage
            },
            ProcessEnvironment = new
            {
                HasOpenAiApiKey = !string.IsNullOrWhiteSpace(processValue),
                Length = string.IsNullOrWhiteSpace(processValue) ? 0 : processValue.Length
            },
            Configuration = new
            {
                HasOpenAiApiKey = !string.IsNullOrWhiteSpace(configuredValue),
                Length = string.IsNullOrWhiteSpace(configuredValue) ? 0 : configuredValue.Length
            },
            Presence = AgentProviderEnvironmentCredential.DescribePresence("OPENAI_API_KEY")
        });
    });

    app.MapGet("/_dev/agentframework/probe-agent/{agentId:guid}", async (
        Guid agentId,
        string? promptMode,
        bool persistTranscript,
        Guid? chatSessionId,
        string? sourceKind,
        string? sourceId,
        string? correlationId,
        string? causationId,
        string? requestedBy,
        string? requestedByKind,
        string? processRunId,
        string? processStepId,
        string? messageId,
        IConfiguration configuration,
        IAgentProviderCredentialResolver providerCredentialResolver,
        ICanDoItAllAgentWorkspaceFactory workspaceFactory) =>
    {
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .FirstOrDefault(item => item.Id == agentId);
        if (agent is null)
        {
            return Results.NotFound(new
            {
                Error = $"Agent '{agentId:D}' was not found in the active workspace."
            });
        }

        var provider = (await workspaceService.ListProvidersAsync())
            .FirstOrDefault(item => item.Id == agent.ProviderProfileId);
        if (provider is null)
        {
            return Results.NotFound(new
            {
                Error = $"Provider '{agent.ProviderProfileId:D}' was not found for agent '{agent.Name}'."
            });
        }

        var resolution = providerCredentialResolver.Resolve(provider);
        var processValue = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var configuredValue = configuration["OPENAI_API_KEY"];
        var effectivePromptMode = string.IsNullOrWhiteSpace(promptMode)
            ? "ok"
            : promptMode.Trim();
        var prompt = "Reply with the single word OK.";
        string? promptSourceSessionId = null;

        if (string.Equals(effectivePromptMode, "latest-process-step-session", StringComparison.OrdinalIgnoreCase))
        {
            var sessions = await workspaceService.ListChatSessionsAsync(agent.Id);
            foreach (var session in sessions
                         .OrderByDescending(item => item.UpdatedAtUtc)
                         .Take(12))
            {
                var workspace = await workspaceService.GetChatAgentWorkspaceAsync(agent.Id, session.Id);
                var latestProcessPrompt = workspace.SelectedSession?.Messages
                    .Where(item => item.Role == ChatMessageRole.User)
                    .OrderByDescending(item => item.CreatedAtUtc)
                    .Select(item => item.Content)
                    .FirstOrDefault(item => item.StartsWith(
                        "You are executing a CanDoItAll process step.",
                        StringComparison.Ordinal));
                if (string.IsNullOrWhiteSpace(latestProcessPrompt))
                {
                    continue;
                }

                prompt = latestProcessPrompt;
                promptSourceSessionId = session.Id.ToString("D");
                break;
            }

            if (string.IsNullOrWhiteSpace(promptSourceSessionId))
            {
                return Results.NotFound(new
                {
                    Error = $"No recent process-step prompt was found for agent '{agent.Name}'."
                });
            }
        }

        object providerProbe;
        try
        {
            var providerResult = await workspaceService.RunProviderTestChatAsync(
                provider.Id,
                new ProviderTestChatRequest(
                    string.Empty,
                    string.Empty,
                    [],
                    "Reply with the single word OK."));
            providerProbe = new
            {
                Succeeded = true,
                providerResult.Model,
                providerResult.ResponseText,
                providerResult.InputTokens,
                providerResult.OutputTokens
            };
        }
        catch (Exception exception)
        {
            providerProbe = new
            {
                Succeeded = false,
                Exception = exception.ToString(),
                InnerException = exception.InnerException?.ToString()
            };
        }

        object agentProbe;
        try
        {
            Guid? probeChatSessionId = null;
            if (chatSessionId.HasValue)
            {
                probeChatSessionId = chatSessionId.Value;
            }
            else if (persistTranscript)
            {
                probeChatSessionId = (await workspaceService.GetOrCreateChatSessionAsync(agent.Id)).Id;
            }

            var executionContext = string.IsNullOrWhiteSpace(sourceKind) &&
                                   string.IsNullOrWhiteSpace(sourceId) &&
                                   string.IsNullOrWhiteSpace(correlationId) &&
                                   string.IsNullOrWhiteSpace(causationId) &&
                                   string.IsNullOrWhiteSpace(requestedBy) &&
                                   string.IsNullOrWhiteSpace(requestedByKind) &&
                                   string.IsNullOrWhiteSpace(processRunId) &&
                                   string.IsNullOrWhiteSpace(processStepId) &&
                                   string.IsNullOrWhiteSpace(messageId)
                ? null
                : new ExecutionInvocationContext(
                    SourceKind: sourceKind ?? string.Empty,
                    SourceId: sourceId ?? string.Empty,
                    CorrelationId: correlationId ?? string.Empty,
                    CausationId: causationId ?? string.Empty,
                    RequestedBy: requestedBy ?? string.Empty,
                    RequestedByKind: requestedByKind ?? string.Empty,
                    MetadataJson: "{}",
                    ProcessRunId: processRunId ?? string.Empty,
                    ProcessStepId: processStepId ?? string.Empty,
                    SchedulerRunId: string.Empty,
                    MessageId: messageId ?? string.Empty);
            var executionResult = await workspaceService.ExecuteRunAsync(
                new ExecutionRunRequest(
                    agent.Id,
                    prompt,
                    probeChatSessionId,
                    Context: executionContext,
                    AutoApprovePendingToolCalls: true));
            agentProbe = new
            {
                Succeeded = true,
                PromptMode = effectivePromptMode,
                PersistTranscript = persistTranscript,
                RequestedChatSessionId = chatSessionId,
                EffectiveChatSessionId = probeChatSessionId,
                PromptLength = prompt.Length,
                PromptSourceSessionId = promptSourceSessionId,
                Context = executionContext,
                executionResult.ExecutionRunId,
                executionResult.ChatSessionId,
                executionResult.ResponseText,
                Metric = new
                {
                    executionResult.Metric.ProviderName,
                    executionResult.Metric.Model,
                    executionResult.Metric.DurationMs,
                    executionResult.Metric.InputTokens,
                    executionResult.Metric.OutputTokens,
                    executionResult.Metric.ToolCalls
                }
            };
        }
        catch (AgentChatRunFailedException exception)
        {
            var detail = await workspaceService.GetExecutionRunDetailAsync(exception.ExecutionRunId);
            agentProbe = new
            {
                Succeeded = false,
                PromptMode = effectivePromptMode,
                PersistTranscript = persistTranscript,
                RequestedChatSessionId = chatSessionId,
                EffectiveChatSessionId = chatSessionId,
                PromptLength = prompt.Length,
                PromptSourceSessionId = promptSourceSessionId,
                Context = new
                {
                    sourceKind,
                    sourceId,
                    correlationId,
                    causationId,
                    requestedBy,
                    requestedByKind,
                    processRunId,
                    processStepId,
                    messageId
                },
                exception.AgentId,
                exception.ExecutionRunId,
                exception.ChatSessionId,
                Exception = exception.ToString(),
                InnerException = exception.InnerException?.ToString(),
                Run = new
                {
                    detail.Run.Id,
                    detail.Run.ProviderName,
                    detail.Run.Model,
                    detail.Run.State,
                    detail.Run.Outcome,
                    detail.Run.ResultSummary
                },
                Log = detail.ExecutionLog
                    .OrderBy(item => item.CreatedAtUtc)
                    .TakeLast(12)
                    .Select(item => new
                    {
                        item.CreatedAtUtc,
                        item.State,
                        item.Phase,
                        item.Message
                    })
                    .ToArray()
            };
        }
        catch (Exception exception)
        {
            agentProbe = new
            {
                Succeeded = false,
                PromptMode = effectivePromptMode,
                PersistTranscript = persistTranscript,
                RequestedChatSessionId = chatSessionId,
                EffectiveChatSessionId = chatSessionId,
                PromptLength = prompt.Length,
                PromptSourceSessionId = promptSourceSessionId,
                Context = new
                {
                    sourceKind,
                    sourceId,
                    correlationId,
                    causationId,
                    requestedBy,
                    requestedByKind,
                    processRunId,
                    processStepId,
                    messageId
                },
                Exception = exception.ToString(),
                InnerException = exception.InnerException?.ToString()
            };
        }

        return Results.Ok(new
        {
            Agent = new
            {
                agent.Id,
                agent.Name,
                agent.ProviderProfileId,
                agent.Model,
                agent.ChatHistoryMode,
                agent.ConfigurationJson
            },
            Provider = new
            {
                provider.Id,
                provider.Name,
                provider.Kind,
                provider.ApiKeyEnvironmentVariable,
                provider.DefaultModel,
                provider.Transport,
                provider.ConfigurationJson
            },
            Resolver = new
            {
                resolution.IsResolved,
                resolution.ResolutionSource,
                resolution.FailureMessage
            },
            ProcessEnvironment = new
            {
                HasOpenAiApiKey = !string.IsNullOrWhiteSpace(processValue),
                Length = string.IsNullOrWhiteSpace(processValue) ? 0 : processValue.Length
            },
            Configuration = new
            {
                HasOpenAiApiKey = !string.IsNullOrWhiteSpace(configuredValue),
                Length = string.IsNullOrWhiteSpace(configuredValue) ? 0 : configuredValue.Length
            },
            Presence = AgentProviderEnvironmentCredential.DescribePresence("OPENAI_API_KEY"),
            ProviderProbe = providerProbe,
            AgentProbe = agentProbe
        });
    });

    app.MapGet("/_dev/agentframework/diagnostics-step/{step}", async (
        string step,
        AiAgentService aiAgentService,
        IAiTechnicalAgentBridge technicalAgentBridge,
        IAgentFrameworkOrganizationCatalogRepairService organizationCatalogRepairService,
        ICanDoItAllAgentWorkspaceFactory workspaceFactory,
        IDbContextFactory<AppDbContext> dbContextFactory) =>
    {
        var stopwatch = Stopwatch.StartNew();
        switch (step.Trim().ToLowerInvariant())
        {
            case "repair":
            {
                await organizationCatalogRepairService.EnsureCurrentOrganizationCatalogAsync();
                stopwatch.Stop();
                return Results.Ok(new
                {
                    Step = "repair",
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                });
            }
            case "sync":
            {
                await technicalAgentBridge.SynchronizeDirectoryProjectionAsync();
                stopwatch.Stop();
                return Results.Ok(new
                {
                    Step = "sync",
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                });
            }
            case "workspace-agents":
            {
                var agents = await workspaceFactory.GetOrganizationWorkspaceService().ListAgentsAsync(includeTemplates: false);
                stopwatch.Stop();
                return Results.Ok(new
                {
                    Step = "workspace-agents",
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    Count = agents.Count,
                    Names = agents.Select(item => item.Name).ToArray()
                });
            }
            case "parties":
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync();
                var parties = await dbContext.Set<Party>()
                    .Where(item => item.PartyType == PartyType.AiAgent)
                    .OrderBy(item => item.DisplayName)
                    .Select(item => new
                    {
                        item.Id,
                        item.DisplayName
                    })
                    .ToListAsync();
                stopwatch.Stop();
                return Results.Ok(new
                {
                    Step = "parties",
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    Count = parties.Count,
                    Parties = parties
                });
            }
            case "summaries":
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync();
                var partyIds = await dbContext.Set<Party>()
                    .Where(item => item.PartyType == PartyType.AiAgent)
                    .OrderBy(item => item.DisplayName)
                    .Select(item => item.Id)
                    .ToListAsync();
                var summaries = await technicalAgentBridge.GetDirectorySummariesAsync(partyIds);
                stopwatch.Stop();
                return Results.Ok(new
                {
                    Step = "summaries",
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    Count = summaries.Count
                });
            }
            case "roster":
            {
                var roster = await aiAgentService.ListAgentDirectoryAsync();
                stopwatch.Stop();
                return Results.Ok(new
                {
                    Step = "roster",
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    Count = roster.Count,
                    Names = roster.Select(item => item.DisplayName).ToArray()
                });
            }
            default:
            {
                stopwatch.Stop();
                return Results.BadRequest(new
                {
                    Error = "Unknown diagnostics step.",
                    SupportedSteps = new[]
                    {
                        "repair",
                        "sync",
                        "workspace-agents",
                        "parties",
                        "summaries",
                        "roster"
                    }
                });
            }
        }
    });
}

app.MapProjectStructureAgentApi();
app.MapCanDoItAllApi();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(CanDoItAll.Web.Composition.ModuleAssemblies.All)
    .AddInteractiveServerRenderMode();
app.MapHealthChecks("/health");

await using (var scope = app.Services.CreateAsyncScope())
{
    var readiness = scope.ServiceProvider.GetRequiredService<IRuntimeReadinessService>();
    readiness.MarkStarting(app.Environment.EnvironmentName, app.Urls.Count > 0 ? app.Urls : ["https://localhost"]);

    var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
    await bootstrapper.EnsureCurrentProfileReadyAsync();

    readiness.MarkReady(app.Environment.EnvironmentName, urls: app.Urls.Count > 0 ? app.Urls : ["https://localhost"]);
}

app.Run();

internal sealed record PostgreSqlDevDatabaseProfileRequest(
    string? DisplayName,
    string? Host,
    int? Port,
    string? DatabaseName,
    string? Username,
    string? Password,
    string? AdminDatabaseName,
    bool? TrustServerCertificate,
    string? WorkspaceRoot,
    bool? Activate);

public partial class Program;


