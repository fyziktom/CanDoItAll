using System.Diagnostics;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Readiness;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Infrastructure;
using IProviderRuntimeAdministrationService =
    CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;

namespace CanDoItAll.Web;

/// <summary>
/// Development-only diagnostic routes under <c>/_dev</c>. The host maps them only in the Development environment.
/// </summary>
internal static class DevelopmentDiagnosticsEndpoints
{
    private const string Tag = "Development diagnostics";

    public static IEndpointRouteBuilder MapDevelopmentDiagnosticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/_dev/runtime", GetRuntime)
            .WithTags(Tag)
            .RequireLocalOrAuthorizedDevelopmentAccess();

        endpoints.MapGet("/_dev/database/selection", GetDatabaseSelectionAsync)
            .WithTags(Tag)
            .RequireLocalOrAuthorizedDevelopmentAccess();

        endpoints.MapPost("/_dev/database/profiles/postgresql", CreatePostgreSqlProfileAsync)
            .WithTags(Tag)
            .RequireLocalOrAuthorizedDevelopmentAccess();

        endpoints.MapPost("/_dev/database/switch/{profileId:guid}", SwitchDatabaseAsync)
            .WithTags(Tag)
            .RequireLocalOrAuthorizedDevelopmentAccess();

        endpoints.MapPost("/_dev/database/seed-profile", SeedProfileAsync)
            .WithTags(Tag)
            .RequireLocalOrAuthorizedDevelopmentAccess();

        endpoints.MapPost("/_dev/projects", CreateProjectAsync)
            .WithTags(Tag)
            .RequireLocalOrAuthorizedDevelopmentAccess();

        endpoints.MapPost("/_dev/agentframework/diagnostics", GetAgentFrameworkDiagnosticsAsync)
            .WithTags(Tag)
            .RequireLocalOrAuthorizedDevelopmentAccess();

        endpoints.MapGet("/_dev/agentframework/credential", GetProviderCredentialDiagnosticsAsync)
            .WithTags(Tag)
            .RequireLocalOrAuthorizedDevelopmentAccess();

        endpoints.MapPost("/_dev/agentframework/probe-agent/{agentId:guid}", ProbeAgentAsync)
            .WithTags(Tag)
            .RequireLocalOrAuthorizedDevelopmentAccess();

        endpoints.MapPost("/_dev/agentframework/diagnostics-step/{step}", RunAgentFrameworkDiagnosticsStepAsync)
            .WithTags(Tag)
            .RequireLocalOrAuthorizedDevelopmentAccess();

        return endpoints;
    }

    /// <summary>
    /// Read the readiness, hot-reload and host-capability state of the running development host.
    /// </summary>
    /// <remarks>
    /// Development diagnostics for local tooling. This route exists only when the host runs in the Development
    /// environment; it is never mapped in other environments and is not part of the versioned API, so its response
    /// can change without notice. It serves only loopback callers (the connection address and any forwarded client
    /// address must both be loopback) or, when API authorization is enabled, a caller whose bearer token has the exact
    /// <c>api.tokens.issue</c> scope. Any other caller receives HTTP 404, as if the route did not exist.
    ///
    /// The JSON object has these members:
    ///
    /// - <c>isReady</c>, <c>environmentName</c>, <c>summary</c>, <c>startedAtUtc</c>, <c>lastChangedAtUtc</c> and
    /// <c>activeUrls</c>: the host readiness snapshot; <c>summary</c> is <c>Starting</c> or <c>Ready</c>, or a failure
    /// summary.
    /// - <c>watchIteration</c>: the <c>DOTNET_WATCH_ITERATION</c> environment variable as an integer, or null when the
    /// host was not started by <c>dotnet watch</c>.
    /// - <c>hotReloadGeneration</c>: number of hot-reload updates applied to this process.
    /// - <c>runtimePid</c>: operating-system process identifier of the host.
    /// - <c>ownerKind</c>, <c>ownerId</c> and <c>serverInstanceId</c>: the <c>CanDoItAllMcpOwnerKind</c>,
    /// <c>CanDoItAllMcpOwnerId</c> and <c>CanDoItAllMcpServerInstanceId</c> configuration values set by the tooling
    /// that launched the host; null when not configured.
    /// - <c>hostCapabilities</c>: the same host-capability snapshot as <c>GET /api/runtime/capabilities</c>.
    ///
    /// The read has no side effects.
    /// </remarks>
    /// <response code="200">The current development runtime diagnostics.</response>
    internal static IResult GetRuntime(
        IRuntimeReadinessService readiness,
        IHostCapabilitySnapshotProvider hostCapabilities,
        IConfiguration configuration)
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
            OwnerKind = configuration["CanDoItAllMcpOwnerKind"],
            OwnerId = configuration["CanDoItAllMcpOwnerId"],
            ServerInstanceId = configuration["CanDoItAllMcpServerInstanceId"],
            snapshot.StartedAtUtc,
            snapshot.LastChangedAtUtc,
            snapshot.ActiveUrls,
            HostCapabilities = hostCapabilities.GetSnapshot()
        });
    }

    /// <summary>
    /// Read which database profile the development host is using and whether another profile waits for a restart.
    /// </summary>
    /// <remarks>
    /// Development diagnostics for local tooling. This route exists only when the host runs in the Development
    /// environment; it is never mapped in other environments and is not part of the versioned API, so its response
    /// can change without notice. It serves only loopback callers (the connection address and any forwarded client
    /// address must both be loopback) or, when API authorization is enabled, a caller whose bearer token has the exact
    /// <c>api.tokens.issue</c> scope. Any other caller receives HTTP 404, as if the route did not exist.
    ///
    /// The JSON object has these members:
    ///
    /// - <c>id</c> and <c>runtimeProfileId</c>: identifier (GUID) of the database profile this process is using.
    /// - <c>pendingRestartProfileId</c>: identifier of the persisted active profile when it differs from the running
    /// profile and takes effect only after a restart; null otherwise, and always null while the running profile is
    /// locked by a runtime override. <c>hasPendingRestartActivation</c> is true exactly when it is not null.
    /// - <c>pendingRestartDisplayName</c> and <c>pendingRestartDescriptor</c>: display name and connection descriptor
    /// of that pending profile; empty strings when there is none.
    /// - <c>displayName</c>, <c>providerKind</c> (JSON integer: 1 PostgreSql, 2 InMemory), <c>sourceKind</c> (JSON
    /// integer: 3 PostgresConnection, 6 InMemory), <c>fingerprint</c> (runtime fingerprint of the profile) and
    /// <c>workspaceRoot</c> (absolute workspace directory of the profile on this host) describe the running profile.
    ///
    /// The read has no side effects. Use <c>POST /_dev/database/switch/{profileId}</c> to change the active profile.
    /// </remarks>
    /// <response code="200">The running database profile and any profile pending a restart.</response>
    internal static async Task<IResult> GetDatabaseSelectionAsync(
        IDatabaseProfileRuntimeAccessor profileAccessor,
        IDatabaseProfileService profileService)
    {
        var profile = profileAccessor.ResolveCurrentProfile();
        var persistedSelection = await profileService.GetCurrentSelectionAsync();
        var pendingRestartProfileId = !profile.Profile.Runtime.LockedByRuntimeOverride &&
            persistedSelection.ActiveProfileId != profile.Profile.Id
                ? persistedSelection.ActiveProfileId
                : (Guid?)null;
        return Results.Ok(new
        {
            profile.Profile.Id,
            RuntimeProfileId = profile.Profile.Id,
            PendingRestartProfileId = pendingRestartProfileId,
            HasPendingRestartActivation = pendingRestartProfileId.HasValue,
            PendingRestartDisplayName = pendingRestartProfileId.HasValue
                ? persistedSelection.DisplayName
                : string.Empty,
            PendingRestartDescriptor = pendingRestartProfileId.HasValue
                ? persistedSelection.Descriptor
                : string.Empty,
            profile.Profile.DisplayName,
            profile.Profile.ProviderKind,
            profile.Profile.SourceKind,
            profile.Profile.Runtime.Fingerprint,
            profile.Profile.Storage.WorkspaceRoot
        });
    }

    /// <summary>
    /// Create a PostgreSQL database profile for the development host, prepare its database and optionally activate it.
    /// </summary>
    /// <remarks>
    /// Development diagnostics for local tooling. This route exists only when the host runs in the Development
    /// environment; it is never mapped in other environments and is not part of the versioned API, so its response
    /// can change without notice. It serves only loopback callers (the connection address and any forwarded client
    /// address must both be loopback) or, when API authorization is enabled, a caller whose bearer token has the exact
    /// <c>api.tokens.issue</c> scope. Any other caller receives HTTP 404, as if the route did not exist.
    ///
    /// Every successful call saves a new profile with a new identifier, creates the database through the maintenance
    /// database when it does not exist yet (an existing database is reused), prepares the application schema and
    /// then, unless <c>activate</c> is false, switches the host to the new profile. A failure after the profile was
    /// saved leaves the saved profile and any created database in place.
    ///
    /// The JSON object returned with HTTP 200 has <c>id</c> (identifier of the new profile), <c>displayName</c>,
    /// <c>providerKind</c> (JSON integer, 1 PostgreSql), <c>sourceKind</c> (JSON integer, 3 PostgresConnection),
    /// <c>fingerprint</c>, <c>workspaceRoot</c>, <c>descriptor</c> (<c>host:port/database</c>) and <c>switch</c>. The
    /// <c>switch</c> member is null when the profile was not activated; otherwise it has <c>generation</c>,
    /// <c>currentProfileId</c>, <c>runtimeProfileId</c>, <c>pendingRestartProfileId</c>, <c>requiresRestart</c>,
    /// <c>runtimeChangedInProcess</c> and <c>message</c>. When <c>requiresRestart</c> is true the process keeps using
    /// its current profile until the host restarts.
    ///
    /// HTTP 400 returns a JSON array of human-readable messages: the database name or user name is missing, or saving
    /// or activating the profile failed. The password is stored with the profile; use only local development
    /// credentials.
    /// </remarks>
    /// <param name="request">PostgreSQL connection settings and whether to activate the new profile.</param>
    /// <response code="200">
    /// The profile was saved and its database prepared; see <c>switch</c> for the activation.
    /// </response>
    internal static async Task<IResult> CreatePostgreSqlProfileAsync(
        PostgreSqlDevDatabaseProfileRequest request,
        IDatabaseProfileService profileService,
        IDatabaseProfileRuntimeAccessor profileAccessor,
        IDatabaseDriverRegistry driverRegistry,
        IAppDatabaseBootstrapper bootstrapper,
        IDatabaseSwitchCoordinator switchCoordinator)
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
                activation.Value.CurrentProfileId,
                activation.Value.RuntimeProfileId,
                activation.Value.PendingRestartProfileId,
                activation.Value.RequiresRestart,
                activation.Value.RuntimeChangedInProcess,
                activation.Value.Message
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
    }

    /// <summary>
    /// Switch the development host to another saved database profile.
    /// </summary>
    /// <remarks>
    /// Development diagnostics for local tooling. This route exists only when the host runs in the Development
    /// environment; it is never mapped in other environments and is not part of the versioned API, so its response
    /// can change without notice. It serves only loopback callers (the connection address and any forwarded client
    /// address must both be loopback) or, when API authorization is enabled, a caller whose bearer token has the exact
    /// <c>api.tokens.issue</c> scope. Any other caller receives HTTP 404, as if the route did not exist.
    ///
    /// The JSON object returned with HTTP 200 has <c>generation</c>, <c>currentProfileId</c>,
    /// <c>runtimeProfileId</c>, <c>pendingRestartProfileId</c>, <c>requiresRestart</c>,
    /// <c>runtimeChangedInProcess</c> and <c>message</c> from the switch, plus <c>activatedProfile</c> and
    /// <c>runtimeProfile</c>, each with <c>id</c>, <c>displayName</c>, <c>fingerprint</c> and <c>workspaceRoot</c>.
    /// When <c>requiresRestart</c> is true the selection is persisted but this process keeps using
    /// <c>runtimeProfile</c> until the host restarts; confirm with <c>GET /_dev/database/selection</c>.
    ///
    /// HTTP 400 returns a JSON array of human-readable messages when the switch is rejected.
    /// </remarks>
    /// <param name="profileId">Identifier of a saved database profile, as returned by the profile routes.</param>
    /// <response code="200">The switch was applied or scheduled for the next restart.</response>
    internal static async Task<IResult> SwitchDatabaseAsync(
        Guid profileId,
        IDatabaseSwitchCoordinator switchCoordinator,
        IDatabaseProfileRuntimeAccessor profileAccessor)
    {
        var switchResult = await switchCoordinator.SwitchAsync(profileId);
        if (switchResult.IsFailure)
        {
            return Results.BadRequest(switchResult.Errors.Select(error => error.Message).ToArray());
        }

        var runtimeProfile = profileAccessor.ResolveCurrentProfile();
        var activatedProfile = profileAccessor.ResolveProfile(switchResult.Value!.CurrentProfileId);
        return Results.Ok(new
        {
            switchResult.Value.Generation,
            switchResult.Value.CurrentProfileId,
            switchResult.Value.RuntimeProfileId,
            switchResult.Value.PendingRestartProfileId,
            switchResult.Value.RequiresRestart,
            switchResult.Value.RuntimeChangedInProcess,
            switchResult.Value.Message,
            ActivatedProfile = new
            {
                activatedProfile.Profile.Id,
                activatedProfile.Profile.DisplayName,
                activatedProfile.Profile.Runtime.Fingerprint,
                activatedProfile.Profile.Storage.WorkspaceRoot
            },
            RuntimeProfile = new
            {
                runtimeProfile.Profile.Id,
                runtimeProfile.Profile.DisplayName,
                runtimeProfile.Profile.Runtime.Fingerprint,
                runtimeProfile.Profile.Storage.WorkspaceRoot
            }
        });
    }

    /// <summary>
    /// Create a labelled seed project and a managed text file in the active database profile of the development host.
    /// </summary>
    /// <remarks>
    /// Development diagnostics for local tooling. This route exists only when the host runs in the Development
    /// environment; it is never mapped in other environments and is not part of the versioned API, so its response
    /// can change without notice. It serves only loopback callers (the connection address and any forwarded client
    /// address must both be loopback) or, when API authorization is enabled, a caller whose bearer token has the exact
    /// <c>api.tokens.issue</c> scope. Any other caller receives HTTP 404, as if the route did not exist.
    ///
    /// Every call creates a new project named <c>{label} Project</c> and writes the text <c>seed:{label}</c> to a
    /// managed file in the <c>profile-seeds</c> category, so that a later database switch can be checked for the
    /// expected data. The JSON object returned with HTTP 200 has <c>value</c> (identifier of the new project),
    /// <c>projectName</c>, <c>managedFileRelativePath</c>, <c>managedFileFullPath</c> (absolute path on this host) and
    /// <c>managedFileContent</c>. HTTP 400 returns a JSON array of human-readable messages when the project is
    /// rejected.
    /// </remarks>
    /// <param name="label">
    /// Label used in the project name and file content; surrounding whitespace is removed. Omitted or blank generates
    /// a label such as <c>Seed 3f2504e</c>: <c>Seed</c>, a space and seven random hexadecimal characters.
    /// </param>
    /// <response code="200">The seed project and managed file were created.</response>
    internal static async Task<IResult> SeedProfileAsync(
        string? label,
        ProjectsService projectsService,
        IManagedArtifactStore managedArtifactStore)
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

        string fileName = PortablePhysicalFileNamePolicy.Encode(seedLabel).PhysicalName;

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
    }

    /// <summary>
    /// Create a throwaway project in the development host and return the address of its structure page.
    /// </summary>
    /// <remarks>
    /// Development diagnostics for local tooling. This route exists only when the host runs in the Development
    /// environment; it is never mapped in other environments and is not part of the versioned API, so its response
    /// can change without notice. It serves only loopback callers (the connection address and any forwarded client
    /// address must both be loopback) or, when API authorization is enabled, a caller whose bearer token has the exact
    /// <c>api.tokens.issue</c> scope. Any other caller receives HTTP 404, as if the route did not exist.
    ///
    /// Every call creates a new project. The JSON object returned with HTTP 200 has <c>projectId</c> and
    /// <c>route</c>, the relative address <c>/projects/{projectId}/structure</c> of the project structure page. For
    /// projects that other clients use, use <c>/api/projects</c> instead. HTTP 400 returns a JSON array of
    /// human-readable messages when the project is rejected.
    /// </remarks>
    /// <param name="name">
    /// Project name; surrounding whitespace is removed. Omitted or blank generates a name such as
    /// <c>Runtime Switch 3f2504e04</c>: <c>Runtime Switch</c>, a space and nine random hexadecimal characters.
    /// </param>
    /// <param name="phase">
    /// Current project phase; surrounding whitespace is removed. Omitted or blank means <c>Execution</c>.
    /// </param>
    /// <response code="200">The project was created.</response>
    internal static async Task<IResult> CreateProjectAsync(
        string? name,
        string? phase,
        ProjectsService projectsService)
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
    }

    /// <summary>
    /// Synchronize the agent directory projection and report the agent parties, bindings and workspace agents it sees.
    /// </summary>
    /// <remarks>
    /// Development diagnostics for local tooling. This route exists only when the host runs in the Development
    /// environment; it is never mapped in other environments and is not part of the versioned API, so its response
    /// can change without notice. It serves only loopback callers (the connection address and any forwarded client
    /// address must both be loopback) or, when API authorization is enabled, a caller whose bearer token has the exact
    /// <c>api.tokens.issue</c> scope. Any other caller receives HTTP 404, as if the route did not exist.
    ///
    /// The call first synchronizes the directory projection that links CRM/HR agent parties to technical agent
    /// definitions, which can write that projection. The JSON object returned with HTTP 200 has the counts
    /// <c>partyCount</c>, <c>bindingCount</c>, <c>workspaceAgentCount</c> and <c>rosterCount</c>, and the lists
    /// <c>parties</c> (each with <c>id</c>, <c>displayName</c> and an optional directory <c>summary</c>),
    /// <c>bindings</c>, <c>workspaceAgents</c> (non-template agents of the organization workspace) and <c>roster</c>.
    /// To time each step separately, use <c>POST /_dev/agentframework/diagnostics-step/{step}</c>.
    /// </remarks>
    /// <response code="200">The synchronized agent directory diagnostics.</response>
    internal static async Task<IResult> GetAgentFrameworkDiagnosticsAsync(
        AiAgentService aiAgentService,
        IAiTechnicalAgentBridge technicalAgentBridge,
        ICanDoItAllAgentWorkspaceFactory workspaceFactory)
    {
        await technicalAgentBridge.SynchronizeDirectoryProjectionAsync();

        var parties = await aiAgentService.ListDiagnosticPartiesAsync();
        var partyIds = parties.Select(item => item.Id).ToList();
        var bindings = await aiAgentService.ListDiagnosticBindingsAsync(partyIds);
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
    }

    /// <summary>
    /// Report whether the credential of the default OpenAI provider profile can be resolved, without returning it.
    /// </summary>
    /// <remarks>
    /// Development diagnostics for local tooling. This route exists only when the host runs in the Development
    /// environment; it is never mapped in other environments and is not part of the versioned API, so its response
    /// can change without notice. It serves only loopback callers (the connection address and any forwarded client
    /// address must both be loopback) or, when API authorization is enabled, a caller whose bearer token has the exact
    /// <c>api.tokens.issue</c> scope. Any other caller receives HTTP 404, as if the route did not exist.
    ///
    /// The route picks the provider profile named <c>OpenAI default</c> (compared case-insensitively) or else the first
    /// OpenAI or Azure OpenAI provider profile. The JSON object returned with HTTP 200 has <c>provider</c> (<c>id</c>,
    /// <c>name</c>, <c>kind</c>, <c>defaultModel</c> and <c>transport</c>), <c>resolverType</c> (the type name of the
    /// credential resolver) and <c>resolver</c> (<c>isResolved</c>, <c>resolutionSource</c> and a redacted
    /// <c>failureMessage</c>). The credential value is never returned. HTTP 404 with a JSON object that has an
    /// <c>error</c> message means that no such provider profile exists in the active workspace. The read has no
    /// side effects.
    /// </remarks>
    /// <response code="200">The credential resolution result of the selected provider profile.</response>
    internal static async Task<IResult> GetProviderCredentialDiagnosticsAsync(
        IAgentProviderCredentialResolver providerCredentialResolver,
        IProviderRuntimeAdministrationService providerAdministration)
    {
        var providers = await providerAdministration.ListProvidersAsync();
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

        return Results.Ok(new
        {
            Provider = new
            {
                provider.Id,
                provider.Name,
                provider.Kind,
                provider.DefaultModel,
                provider.Transport
            },
            ResolverType = providerCredentialResolver.GetType().FullName,
            Resolver = new
            {
                resolution.IsResolved,
                resolution.ResolutionSource,
                FailureMessage = WorkflowExecutorRedaction.RedactText(resolution.FailureMessage)
            }
        });
    }

    /// <summary>
    /// Run a test chat against an agent's provider and a real agent execution run, and report both outcomes.
    /// </summary>
    /// <remarks>
    /// Development diagnostics for local tooling. This route exists only when the host runs in the Development
    /// environment; it is never mapped in other environments and is not part of the versioned API, so its response
    /// can change without notice. It serves only loopback callers (the connection address and any forwarded client
    /// address must both be loopback) or, when API authorization is enabled, a caller whose bearer token has the exact
    /// <c>api.tokens.issue</c> scope. Any other caller receives HTTP 404, as if the route did not exist.
    ///
    /// The probe is not read-only: it sends a test chat to the agent's provider and then executes an agent run, which
    /// calls the model, can use the agent's tools with their effects, is recorded as an execution run and, with
    /// <c>persistTranscript</c> or <c>chatSessionId</c>, writes to a chat session. With <c>autoApprove</c> true,
    /// pending tool calls are approved automatically. Both calls can incur provider cost.
    ///
    /// The JSON object returned with HTTP 200 has <c>agent</c>, <c>provider</c>, <c>resolver</c> (credential
    /// resolution, redacted), <c>providerProbe</c> (<c>succeeded</c>, the model, response length and token counts, or a
    /// failure type and redacted message) and <c>agentProbe</c> (the run settings, <c>succeeded</c>, the execution run
    /// and chat session identifiers, the response length and run metrics; on failure the failure type, a redacted
    /// message and, when the run exists, its state and last twelve log entries). A failed probe still returns HTTP 200;
    /// read <c>succeeded</c>. HTTP 404 with a JSON object that has an <c>error</c> message means the agent, its
    /// provider profile or, for the <c>latest-process-step-session</c> prompt mode, a recent process-step prompt was
    /// not found.
    /// </remarks>
    /// <param name="agentId">Identifier of the technical agent in the organization workspace.</param>
    /// <param name="promptMode">
    /// <c>ok</c> (the default when omitted or blank) sends the prompt <c>Reply with the single word OK.</c>;
    /// <c>latest-process-step-session</c> (compared case-insensitively) replays the most recent process-step prompt
    /// found in the agent's twelve most recently updated chat sessions.
    /// </param>
    /// <param name="persistTranscript">
    /// Required. When <c>chatSessionId</c> is omitted, true runs the agent probe in a chat session of the agent
    /// (an existing one or a new one) so that the exchange is kept, and false runs it without a chat session.
    /// </param>
    /// <param name="autoApprove">True approves pending tool calls automatically. Omitted means false.</param>
    /// <param name="chatSessionId">Identifier of an existing chat session of the agent to run the probe in.</param>
    /// <param name="sourceKind">Execution-context source kind recorded on the run. Optional.</param>
    /// <param name="sourceId">Execution-context source identifier recorded on the run. Optional.</param>
    /// <param name="correlationId">Execution-context correlation identifier recorded on the run. Optional.</param>
    /// <param name="causationId">Execution-context causation identifier recorded on the run. Optional.</param>
    /// <param name="requestedBy">Execution-context requester recorded on the run. Optional.</param>
    /// <param name="requestedByKind">Execution-context requester kind recorded on the run. Optional.</param>
    /// <param name="processRunId">Process run identifier recorded in the execution context. Optional.</param>
    /// <param name="processStepId">Process step identifier recorded in the execution context. Optional.</param>
    /// <param name="messageId">Message identifier recorded in the execution context. Optional.</param>
    /// <response code="200">
    /// Both probes ran; read <c>providerProbe.succeeded</c> and <c>agentProbe.succeeded</c>.
    /// </response>
    internal static async Task<IResult> ProbeAgentAsync(
        Guid agentId,
        string? promptMode,
        bool persistTranscript,
        bool? autoApprove,
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
        IAgentProviderCredentialResolver providerCredentialResolver,
        IProviderRuntimeAdministrationService providerAdministration,
        ICanDoItAllAgentWorkspaceFactory workspaceFactory)
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

        var provider = (await providerAdministration.ListProvidersAsync())
            .FirstOrDefault(item => item.Id == agent.ProviderProfileId);
        if (provider is null)
        {
            return Results.NotFound(new
            {
                Error = $"Provider '{agent.ProviderProfileId:D}' was not found for agent '{agent.Name}'."
            });
        }

        var resolution = providerCredentialResolver.Resolve(provider);
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
            var providerResult = await providerAdministration.RunProviderTestChatAsync(
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
                ResponseCharacters = providerResult.ResponseText.Length,
                providerResult.InputTokens,
                providerResult.OutputTokens
            };
        }
        catch (Exception exception)
        {
            providerProbe = new
            {
                Succeeded = false,
                FailureType = exception.GetType().Name,
                FailureMessage = WorkflowExecutorRedaction.RedactText(exception.Message)
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
                    AgentExecutionOperationId.New(),
                    probeChatSessionId,
                    Context: executionContext,
                    AutoApprovePendingToolCalls: autoApprove == true));
            agentProbe = new
            {
                Succeeded = true,
                PromptMode = effectivePromptMode,
                PersistTranscript = persistTranscript,
                AutoApprove = autoApprove == true,
                RequestedChatSessionId = chatSessionId,
                EffectiveChatSessionId = probeChatSessionId,
                PromptLength = prompt.Length,
                PromptSourceSessionId = promptSourceSessionId,
                Context = executionContext,
                executionResult.ExecutionRunId,
                executionResult.ChatSessionId,
                ResponseCharacters = executionResult.ResponseText.Length,
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
                AutoApprove = autoApprove == true,
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
                FailureType = exception.GetType().Name,
                FailureMessage = WorkflowExecutorRedaction.RedactText(exception.Message),
                Run = new
                {
                    detail.Run.Id,
                    detail.Run.ProviderName,
                    detail.Run.Model,
                    detail.Run.State,
                    detail.Run.Outcome,
                    ResultSummary = WorkflowExecutorRedaction.RedactText(detail.Run.ResultSummary)
                },
                Log = detail.ExecutionLog
                    .OrderBy(item => item.CreatedAtUtc)
                    .TakeLast(12)
                    .Select(item => new
                    {
                        item.CreatedAtUtc,
                        item.State,
                        item.Phase,
                        Message = WorkflowExecutorRedaction.RedactText(item.Message)
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
                AutoApprove = autoApprove == true,
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
                FailureType = exception.GetType().Name,
                FailureMessage = WorkflowExecutorRedaction.RedactText(exception.Message)
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
                agent.ChatHistoryMode
            },
            Provider = new
            {
                provider.Id,
                provider.Name,
                provider.Kind,
                provider.DefaultModel,
                provider.Transport
            },
            Resolver = new
            {
                resolution.IsResolved,
                resolution.ResolutionSource,
                FailureMessage = WorkflowExecutorRedaction.RedactText(resolution.FailureMessage)
            },
            ProviderProbe = providerProbe,
            AgentProbe = agentProbe
        });
    }

    /// <summary>
    /// Run and time one step of the agent directory diagnostics.
    /// </summary>
    /// <remarks>
    /// Development diagnostics for local tooling. This route exists only when the host runs in the Development
    /// environment; it is never mapped in other environments and is not part of the versioned API, so its response
    /// can change without notice. It serves only loopback callers (the connection address and any forwarded client
    /// address must both be loopback) or, when API authorization is enabled, a caller whose bearer token has the exact
    /// <c>api.tokens.issue</c> scope. Any other caller receives HTTP 404, as if the route did not exist.
    ///
    /// The JSON object returned with HTTP 200 always has <c>step</c> and <c>elapsedMilliseconds</c>. The
    /// <c>workspace-agents</c> and <c>roster</c> steps add <c>count</c> and <c>names</c>, <c>parties</c> adds
    /// <c>count</c> and <c>parties</c>, and <c>summaries</c> adds <c>count</c>. The <c>repair</c> and <c>sync</c> steps
    /// write: they repair the organization agent catalog and synchronize the agent directory projection. HTTP 400
    /// returns a JSON object with <c>error</c> and <c>supportedSteps</c> for an unknown step.
    /// </remarks>
    /// <param name="step">
    /// The step to run, compared case-insensitively after trimming: <c>repair</c>, <c>sync</c>,
    /// <c>workspace-agents</c>, <c>parties</c>, <c>summaries</c> or <c>roster</c>.
    /// </param>
    /// <response code="200">The step ran; the body reports its duration and results.</response>
    internal static async Task<IResult> RunAgentFrameworkDiagnosticsStepAsync(
        string step,
        AiAgentService aiAgentService,
        IAiTechnicalAgentBridge technicalAgentBridge,
        IAgentFrameworkOrganizationCatalogRepairService organizationCatalogRepairService,
        ICanDoItAllAgentWorkspaceFactory workspaceFactory)
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
                var parties = await aiAgentService.ListDiagnosticPartiesAsync();
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
                var partyIds = (await aiAgentService.ListDiagnosticPartiesAsync()).Select(item => item.Id).ToList();
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
    }
}
