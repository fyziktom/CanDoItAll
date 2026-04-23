using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.Composition;

public static class RuntimeHostServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllRuntimeModules(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSecurityModule();
        services.AddWorkspaceModule();
        services.AddProjectsModule();
        services.AddWorkbenchModule();
        services.AddResourcesModule();
        services.AddPromptsModule();
        services.AddFactoryModule();
        services.AddProcessesModule(configuration);
        services.AddValidationModule();
        services.AddTestLabModule();
        services.AddActivityModule();
        services.AddAgentFrameworkModule(configuration);
        services.AddAutomationModule(configuration);
        services.AddCollaborationModule();
        services.AddCrmHrModule();
        return services;
    }

    public static IServiceCollection AddCanDoItAllRuntimeDatabaseSwitching(this IServiceCollection services)
    {
        services.AddSingleton<IAppDatabaseBootstrapper, AppDatabaseBootstrapper>();
        services.AddSingleton<IDatabaseSwitchCoordinator, DatabaseSwitchCoordinator>();
        return services;
    }
}

public sealed class AppDatabaseBootstrapper(
    IDatabaseProfileRuntimeAccessor profileAccessor,
    ISwitchableAppDbContextFactory dbContextFactory,
    IAgentProviderCredentialResolver providerCredentialResolver,
    ILogger<AppDatabaseBootstrapper> logger) : IAppDatabaseBootstrapper
{
    private static readonly Guid ManagedDeliveryUnitPartyId = Guid.Parse("10BE49B1-EF4D-4A58-B9EA-B3F7D40F31A1");
    private static readonly Guid ManagedProductOwnerPartyId = Guid.Parse("A6BBAD2B-9D18-40EA-95B5-6D73C20C3078");
    private static readonly Guid ManagedDeliveryManagerPartyId = Guid.Parse("4B4718D5-4F86-4A6A-9BE7-3ACCA7E0F2AB");
    private static readonly Guid ManagedDeliveryUnitRoleId = Guid.Parse("1A8A7BB6-10B5-4D18-A91F-00F25E045DBF");
    private static readonly Guid ManagedProductOwnerRoleId = Guid.Parse("DBF3B8E6-77D2-49D5-924A-74CA8FFFBFD3");
    private static readonly Guid ManagedDeliveryManagerRoleId = Guid.Parse("2D9DF6AC-8B49-43EA-960E-8B912A758296");
    private static readonly Guid ManagedProductOwnerProfileId = Guid.Parse("61C29FAE-C560-4C2D-993E-BE842FD635FB");
    private static readonly Guid ManagedDeliveryManagerProfileId = Guid.Parse("E0EBEC09-C37B-4F42-9FA4-1B2DDAC20572");
    private const string ManagedSqliteOpenAiDefaultProviderName = "OpenAI default";
    private const string ManagedSqliteOpenAiChatCompletionsProviderName = "OpenAI chat completions";
    private static readonly Guid ManagedSqliteRemoteOllamaProviderId = Guid.Parse("2DB76580-21A4-B156-81A7-68DC0EE7513C");
    private static readonly IReadOnlySet<string> ManagedSqliteOpenAiProviderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ManagedSqliteOpenAiDefaultProviderName,
        ManagedSqliteOpenAiChatCompletionsProviderName
    };
    private static readonly IReadOnlyList<string> ManagedSqliteRemoteOllamaSuggestedModels =
    [
        "gptoss32k:latest",
        "gptoss64k:latest",
        "gpt-oss:20b",
        "qwen3.5:9b",
        "phi4-16k:latest"
    ];

    private const string ManagedSqliteBootstrapActor = "managed-sqlite-bootstrap";
    private const string ManagedSqliteSeedMarker = "managedSeedVersion";
    private const string ManagedSqliteRemoteOllamaProviderName = "Remote Ollama";
    private const string ManagedSqliteRemoteOllamaBaseUrl = "http://192.168.10.132:11434";
    private const string ManagedSqliteRemoteOllamaModel = "gptoss32k:latest";
    private const string ManagedSqliteProviderSchemaVersion = "1.0";
    private const int ManagedSqliteRemoteOllamaTimeoutSeconds = 600;

    public Task EnsureCurrentProfileReadyAsync(CancellationToken cancellationToken = default)
    {
        return EnsureProfileReadyAsync(profileAccessor.ResolveCurrentProfile(), cancellationToken);
    }

    public async Task EnsureProfileReadyAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Ensuring runtime database profile {ProfileId} ({DisplayName}) is ready. Provider={ProviderKind}, Source={SourceKind}.",
            profile.Profile.Id,
            profile.Profile.DisplayName,
            profile.Profile.ProviderKind,
            profile.Profile.SourceKind);

        await using var dbContext = await dbContextFactory.CreateDbContextForProfileAsync(profile, cancellationToken);
        if (!dbContext.Database.IsRelational())
        {
            logger.LogInformation(
                "Ensuring non-relational database profile {ProfileId} is created.",
                profile.Profile.Id);
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            logger.LogInformation(
                "Non-relational database profile {ProfileId} is ready.",
                profile.Profile.Id);
            return;
        }

        logger.LogInformation(
            "Preparing legacy SQLite compatibility for profile {ProfileId}.",
            profile.Profile.Id);
        await CanDoItAllDatabaseMigrationBootstrap.PrepareLegacySqliteAsync(dbContext, logger, cancellationToken);
        logger.LogInformation(
            "Releasing stale SQLite EF migration locks for profile {ProfileId}.",
            profile.Profile.Id);
        await CanDoItAllDatabaseMigrationBootstrap.ReleaseStaleSqliteMigrationLockAsync(dbContext, logger, cancellationToken);
        logger.LogInformation(
            "Applying EF migrations for profile {ProfileId}.",
            profile.Profile.Id);
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation(
            "Ensuring CRM/HR schema for profile {ProfileId}.",
            profile.Profile.Id);
        await CrmHrSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        logger.LogInformation(
            "Ensuring managed SQLite staffing bootstrap for profile {ProfileId}.",
            profile.Profile.Id);
        await EnsureManagedSqliteStaffingBootstrapAsync(profile, dbContext, cancellationToken);
        logger.LogInformation(
            "Ensuring managed SQLite agent provider bootstrap for profile {ProfileId}.",
            profile.Profile.Id);
        await EnsureManagedSqliteAgentProviderBootstrapAsync(profile, dbContext, cancellationToken);
        logger.LogInformation(
            "Runtime database profile {ProfileId} is ready.",
            profile.Profile.Id);
    }

    private async Task EnsureManagedSqliteStaffingBootstrapAsync(
        ResolvedDatabaseProfile profile,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (profile.Profile.SourceKind != DatabaseProfileSourceKind.ManagedSqlite)
        {
            return;
        }

        var timestamp = DateTimeOffset.UtcNow;
        var parties = await dbContext.Set<Party>()
            .Where(item =>
                item.Id == ManagedDeliveryUnitPartyId ||
                item.Id == ManagedProductOwnerPartyId ||
                item.Id == ManagedDeliveryManagerPartyId)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var roles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item =>
                item.Id == ManagedDeliveryUnitRoleId ||
                item.Id == ManagedProductOwnerRoleId ||
                item.Id == ManagedDeliveryManagerRoleId)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var profiles = await dbContext.Set<WorkforceProfile>()
            .Where(item =>
                item.Id == ManagedProductOwnerProfileId ||
                item.Id == ManagedDeliveryManagerProfileId)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var changed = false;

        if (!parties.ContainsKey(ManagedDeliveryUnitPartyId))
        {
            dbContext.Set<Party>().Add(new Party
            {
                Id = ManagedDeliveryUnitPartyId,
                PartyType = PartyType.OrganizationUnit,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = "Managed Demo Delivery Leadership",
                LegalName = "Managed Demo Delivery Leadership",
                PreferredName = "Managed Demo Delivery Leadership",
                ExternalCode = "managed-sqlite-demo-delivery-unit",
                Summary = "Bootstrap delivery unit for managed SQLite staffing and process-start review flows.",
                Notes = "Created automatically so process launch review has factual CRM-HR delivery coverage in managed SQLite profiles.",
                TagsJson = "[\"managed-sqlite\",\"demo\",\"delivery-unit\"]",
                Region = "Remote",
                CountryCode = "US",
                TimeZone = "America/La_Paz",
                ExtendedDataJson = "{}",
                LastChangedBy = "managed-sqlite-bootstrap",
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            });
            changed = true;
        }

        if (!parties.ContainsKey(ManagedProductOwnerPartyId))
        {
            dbContext.Set<Party>().Add(new Party
            {
                Id = ManagedProductOwnerPartyId,
                PartyType = PartyType.Person,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = "Parker Product",
                LegalName = "Parker Product",
                PreferredName = "Parker",
                ExternalCode = "managed-sqlite-demo-product-owner",
                Summary = "Managed SQLite bootstrap product owner used for staffing suggestions and process-launch validation.",
                Notes = "Created automatically so product-owner process roles can be matched from CRM-HR without guesswork.",
                TagsJson = "[\"managed-sqlite\",\"demo\",\"product-owner\"]",
                Region = "Remote",
                CountryCode = "US",
                TimeZone = "America/La_Paz",
                ExtendedDataJson = "{}",
                LastChangedBy = "managed-sqlite-bootstrap",
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            });
            changed = true;
        }

        if (!parties.ContainsKey(ManagedDeliveryManagerPartyId))
        {
            dbContext.Set<Party>().Add(new Party
            {
                Id = ManagedDeliveryManagerPartyId,
                PartyType = PartyType.Person,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = "Devon Delivery",
                LegalName = "Devon Delivery",
                PreferredName = "Devon",
                ExternalCode = "managed-sqlite-demo-delivery-manager",
                Summary = "Managed SQLite bootstrap delivery manager used for staffing suggestions and process-launch validation.",
                Notes = "Created automatically so delivery-manager process roles can be matched from CRM-HR without guesswork.",
                TagsJson = "[\"managed-sqlite\",\"demo\",\"delivery-manager\"]",
                Region = "Remote",
                CountryCode = "US",
                TimeZone = "America/La_Paz",
                ExtendedDataJson = "{}",
                LastChangedBy = "managed-sqlite-bootstrap",
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            });
            changed = true;
        }

        if (!roles.ContainsKey(ManagedDeliveryUnitRoleId))
        {
            dbContext.Set<PartyRoleAssignment>().Add(new PartyRoleAssignment
            {
                Id = ManagedDeliveryUnitRoleId,
                PartyId = ManagedDeliveryUnitPartyId,
                RoleKind = PartyRoleKind.DeliveryUnit,
                Title = "Delivery leadership",
                IsPrimary = true,
                Notes = "Managed SQLite bootstrap delivery unit role."
            });
            changed = true;
        }

        if (!roles.ContainsKey(ManagedProductOwnerRoleId))
        {
            dbContext.Set<PartyRoleAssignment>().Add(new PartyRoleAssignment
            {
                Id = ManagedProductOwnerRoleId,
                PartyId = ManagedProductOwnerPartyId,
                RoleKind = PartyRoleKind.Employee,
                Title = "Product owner",
                IsPrimary = true,
                Notes = "Managed SQLite bootstrap workforce role."
            });
            changed = true;
        }

        if (!roles.ContainsKey(ManagedDeliveryManagerRoleId))
        {
            dbContext.Set<PartyRoleAssignment>().Add(new PartyRoleAssignment
            {
                Id = ManagedDeliveryManagerRoleId,
                PartyId = ManagedDeliveryManagerPartyId,
                RoleKind = PartyRoleKind.Employee,
                Title = "Delivery manager",
                IsPrimary = true,
                Notes = "Managed SQLite bootstrap workforce role."
            });
            changed = true;
        }

        if (!profiles.ContainsKey(ManagedProductOwnerProfileId))
        {
            dbContext.Set<WorkforceProfile>().Add(new WorkforceProfile
            {
                Id = ManagedProductOwnerProfileId,
                PartyId = ManagedProductOwnerPartyId,
                WorkforceKind = WorkforceKind.Employee,
                EmployeeCode = "MS-PO-001",
                JobTitle = "Product owner",
                Discipline = "Product management",
                Seniority = "Lead",
                HomeUnitPartyId = ManagedDeliveryUnitPartyId,
                Location = "Remote",
                TimeZone = "America/La_Paz",
                CapacityHoursPerWeek = 40m,
                Status = "Active",
                ExtendedDataJson = "{}",
                Notes = "Managed SQLite bootstrap workforce record for process-start staffing review."
            });
            changed = true;
        }

        if (!profiles.ContainsKey(ManagedDeliveryManagerProfileId))
        {
            dbContext.Set<WorkforceProfile>().Add(new WorkforceProfile
            {
                Id = ManagedDeliveryManagerProfileId,
                PartyId = ManagedDeliveryManagerPartyId,
                WorkforceKind = WorkforceKind.Employee,
                EmployeeCode = "MS-DM-001",
                JobTitle = "Delivery manager",
                Discipline = "Program delivery",
                Seniority = "Lead",
                HomeUnitPartyId = ManagedDeliveryUnitPartyId,
                Location = "Remote",
                TimeZone = "America/La_Paz",
                CapacityHoursPerWeek = 40m,
                Status = "Active",
                ExtendedDataJson = "{}",
                Notes = "Managed SQLite bootstrap workforce record for process-start staffing review."
            });
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Seeded managed SQLite staffing bootstrap data for profile {ProfileId}.",
            profile.Profile.Id);
    }

    private async Task EnsureManagedSqliteAgentProviderBootstrapAsync(
        ResolvedDatabaseProfile profile,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (profile.Profile.SourceKind != DatabaseProfileSourceKind.ManagedSqlite)
        {
            return;
        }

        var timestamp = DateTimeOffset.UtcNow;
        var changed = false;
        var remoteProvider = await dbContext.Set<CanDoItAll.Modules.Workspace.ProviderProfile>()
            .SingleOrDefaultAsync(item => item.Id == ManagedSqliteRemoteOllamaProviderId, cancellationToken);
        if (remoteProvider is null)
        {
            dbContext.Set<CanDoItAll.Modules.Workspace.ProviderProfile>().Add(new CanDoItAll.Modules.Workspace.ProviderProfile
            {
                Id = ManagedSqliteRemoteOllamaProviderId,
                Name = ManagedSqliteRemoteOllamaProviderName,
                ProviderKind = CanDoItAll.Modules.Workspace.ProviderKind.OllamaRemote,
                ConnectorPluginKey = OllamaRemoteProviderAdapter.PluginKey,
                ConfigSchemaVersion = ManagedSqliteProviderSchemaVersion,
                BaseUrl = ManagedSqliteRemoteOllamaBaseUrl,
                DefaultModel = ManagedSqliteRemoteOllamaModel,
                TimeoutSeconds = ManagedSqliteRemoteOllamaTimeoutSeconds,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsToolCalling = true,
                SupportsStructuredOutput = false,
                SupportsVision = false,
                LastHealthStatus = "Bootstrap fallback",
                LastHealthCheckAtUtc = null,
                ExtraSettingsJson = JsonSerializer.Serialize(new
                {
                    history = "framework-managed",
                    connectorPluginKey = OllamaRemoteProviderAdapter.PluginKey,
                    configSchemaVersion = ManagedSqliteProviderSchemaVersion,
                    timeoutSeconds = ManagedSqliteRemoteOllamaTimeoutSeconds
                })
            });
            changed = true;
        }
        else
        {
            changed |= UpdateManagedSqliteRemoteOllamaProvider(remoteProvider);
        }

        var settings = await dbContext.Set<WorkspaceSettings>()
            .FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            dbContext.Set<WorkspaceSettings>().Add(new WorkspaceSettings
            {
                DefaultProviderProfileId = ManagedSqliteRemoteOllamaProviderId,
                WorkspaceName = "CanDoItAll",
                DefaultPromptOutputFormat = "Markdown",
                Notes = "Managed SQLite bootstrap default provider.",
                UpdatedAtUtc = timestamp
            });
            changed = true;
        }
        else if (settings.DefaultProviderProfileId != ManagedSqliteRemoteOllamaProviderId)
        {
            settings.DefaultProviderProfileId = ManagedSqliteRemoteOllamaProviderId;
            settings.UpdatedAtUtc = timestamp;
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Seeded managed SQLite Remote Ollama provider bootstrap for profile {ProfileId}.",
                profile.Profile.Id);
        }

        var workspaceRoot = profile.Profile.Storage.WorkspaceRoot;
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            return;
        }

        var store = new FileSandboxWorkspaceStore(
            workspaceRoot,
            WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N")));
        var catalogChanged = false;
        await store.UpdateCatalogAsync(catalog =>
        {
            var remoteCatalogProvider = CreateManagedSqliteRemoteOllamaCatalogProvider();
            var providerIdsToRedirect = catalog.Providers
                .Where(item => ManagedSqliteOpenAiProviderNames.Contains(item.Name))
                .Select(item => item.Id)
                .ToHashSet();

            var updatedProviders = catalog.Providers
                .Where(item => item.Id != ManagedSqliteRemoteOllamaProviderId)
                .Append(remoteCatalogProvider)
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var updatedAgents = catalog.Agents
                .Select(agent => ShouldRedirectManagedSqliteAgent(agent, providerIdsToRedirect)
                    ? agent with
                    {
                        ProviderProfileId = ManagedSqliteRemoteOllamaProviderId,
                        Model = ManagedSqliteRemoteOllamaModel,
                        UpdatedAtUtc = timestamp
                    }
                    : agent)
                .ToList();

            catalogChanged =
                !updatedProviders.SequenceEqual(catalog.Providers) ||
                !updatedAgents.SequenceEqual(catalog.Agents);
            return catalogChanged
                ? catalog with
                {
                    Providers = updatedProviders,
                    Agents = updatedAgents
                }
                : catalog;
        }, cancellationToken);

        if (catalogChanged)
        {
            logger.LogInformation(
                "Remapped managed SQLite seeded agents to Remote Ollama for profile {ProfileId}.",
                profile.Profile.Id);
        }
    }

    private static bool UpdateManagedSqliteRemoteOllamaProvider(CanDoItAll.Modules.Workspace.ProviderProfile provider)
    {
        var changed = false;
        if (!string.Equals(provider.Name, ManagedSqliteRemoteOllamaProviderName, StringComparison.Ordinal))
        {
            provider.Name = ManagedSqliteRemoteOllamaProviderName;
            changed = true;
        }

        if (provider.ProviderKind != CanDoItAll.Modules.Workspace.ProviderKind.OllamaRemote)
        {
            provider.ProviderKind = CanDoItAll.Modules.Workspace.ProviderKind.OllamaRemote;
            changed = true;
        }

        if (!string.Equals(provider.ConnectorPluginKey, OllamaRemoteProviderAdapter.PluginKey, StringComparison.Ordinal))
        {
            provider.ConnectorPluginKey = OllamaRemoteProviderAdapter.PluginKey;
            changed = true;
        }

        if (!string.Equals(provider.ConfigSchemaVersion, ManagedSqliteProviderSchemaVersion, StringComparison.Ordinal))
        {
            provider.ConfigSchemaVersion = ManagedSqliteProviderSchemaVersion;
            changed = true;
        }

        if (!string.Equals(provider.BaseUrl, ManagedSqliteRemoteOllamaBaseUrl, StringComparison.Ordinal))
        {
            provider.BaseUrl = ManagedSqliteRemoteOllamaBaseUrl;
            changed = true;
        }

        if (!string.Equals(provider.DefaultModel, ManagedSqliteRemoteOllamaModel, StringComparison.Ordinal))
        {
            provider.DefaultModel = ManagedSqliteRemoteOllamaModel;
            changed = true;
        }

        if (provider.TimeoutSeconds != ManagedSqliteRemoteOllamaTimeoutSeconds)
        {
            provider.TimeoutSeconds = ManagedSqliteRemoteOllamaTimeoutSeconds;
            changed = true;
        }

        if (!provider.IsEnabled)
        {
            provider.IsEnabled = true;
            changed = true;
        }

        if (!provider.SupportsStreaming)
        {
            provider.SupportsStreaming = true;
            changed = true;
        }

        if (!provider.SupportsToolCalling)
        {
            provider.SupportsToolCalling = true;
            changed = true;
        }

        if (provider.SupportsStructuredOutput)
        {
            provider.SupportsStructuredOutput = false;
            changed = true;
        }

        if (provider.SupportsVision)
        {
            provider.SupportsVision = false;
            changed = true;
        }

        if (provider.ApiKeySecretId.HasValue)
        {
            provider.ApiKeySecretId = null;
            changed = true;
        }

        if (!string.Equals(provider.LastHealthStatus, "Bootstrap fallback", StringComparison.Ordinal))
        {
            provider.LastHealthStatus = "Bootstrap fallback";
            changed = true;
        }

        var expectedExtraSettingsJson = JsonSerializer.Serialize(new
        {
            history = "framework-managed",
            connectorPluginKey = OllamaRemoteProviderAdapter.PluginKey,
            configSchemaVersion = ManagedSqliteProviderSchemaVersion,
            timeoutSeconds = ManagedSqliteRemoteOllamaTimeoutSeconds
        });
        if (!string.Equals(provider.ExtraSettingsJson, expectedExtraSettingsJson, StringComparison.Ordinal))
        {
            provider.ExtraSettingsJson = expectedExtraSettingsJson;
            changed = true;
        }

        return changed;
    }

    private static CanDoItAll.AgentFramework.Models.ProviderProfile CreateManagedSqliteRemoteOllamaCatalogProvider()
    {
        return new CanDoItAll.AgentFramework.Models.ProviderProfile(
            ManagedSqliteRemoteOllamaProviderId,
            ManagedSqliteRemoteOllamaProviderName,
            CanDoItAll.AgentFramework.Models.ProviderKind.Ollama,
            ManagedSqliteRemoteOllamaBaseUrl,
            string.Empty,
            ManagedSqliteRemoteOllamaModel,
            ProviderTransportKind.ChatCompletions,
            true,
            true,
            true,
            true,
            false,
            JsonSerializer.Serialize(new { history = "framework-managed" }),
            "Managed SQLite fallback provider used when OPENAI_API_KEY is unavailable.",
            "Bootstrap fallback",
            null,
            ManagedSqliteRemoteOllamaSuggestedModels);
    }

    private async Task<bool> HasManagedSqliteOpenAiCredentialAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var configuredOpenAiProviders = await dbContext.Set<CanDoItAll.Modules.Workspace.ProviderProfile>()
            .Where(item =>
                item.Name == ManagedSqliteOpenAiDefaultProviderName ||
                item.Name == ManagedSqliteOpenAiChatCompletionsProviderName)
            .ToListAsync(cancellationToken);

        if (configuredOpenAiProviders.Count == 0)
        {
            return providerCredentialResolver.Resolve(CreateManagedSqliteBootstrapOpenAiProvider()).IsResolved;
        }

        return configuredOpenAiProviders
            .Select(MapManagedSqliteBootstrapProvider)
            .Any(provider => providerCredentialResolver.Resolve(provider).IsResolved);
    }

    private static CanDoItAll.AgentFramework.Models.ProviderProfile CreateManagedSqliteBootstrapOpenAiProvider()
    {
        return new CanDoItAll.AgentFramework.Models.ProviderProfile(
            Guid.Empty,
            ManagedSqliteOpenAiDefaultProviderName,
            CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            "gpt-4.1",
            ProviderTransportKind.Responses,
            true,
            true,
            true,
            false,
            true,
            JsonSerializer.Serialize(new { history = "service-managed" }),
            "Managed SQLite bootstrap credential probe.",
            "Not checked",
            null,
            ["gpt-4.1"]);
    }

    private static CanDoItAll.AgentFramework.Models.ProviderProfile MapManagedSqliteBootstrapProvider(
        CanDoItAll.Modules.Workspace.ProviderProfile provider)
    {
        var mappedKind = provider.ConnectorPluginKey switch
        {
            ScenarioHarnessProviderAdapter.PluginKey => CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi,
            OpenAiProviderAdapter.PluginKey => CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi,
            _ => CanDoItAll.AgentFramework.Models.ProviderKind.Ollama
        };
        var mappedTransport = provider.ConnectorPluginKey switch
        {
            ScenarioHarnessProviderAdapter.PluginKey => ProviderTransportKind.Responses,
            OpenAiProviderAdapter.PluginKey => ProviderTransportKind.Responses,
            _ => ProviderTransportKind.ChatCompletions
        };

        return new CanDoItAll.AgentFramework.Models.ProviderProfile(
            provider.Id,
            provider.Name,
            mappedKind,
            provider.BaseUrl,
            provider.ApiKeySecretId.HasValue
                ? $"secret:{provider.ApiKeySecretId.Value:D}"
                : "OPENAI_API_KEY",
            provider.DefaultModel,
            mappedTransport,
            provider.IsEnabled,
            provider.SupportsStreaming,
            provider.SupportsToolCalling,
            mappedKind == CanDoItAll.AgentFramework.Models.ProviderKind.Ollama,
            mappedKind == CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi,
            BuildManagedSqliteBootstrapProviderConfigurationJson(provider),
            "Managed SQLite bootstrap credential probe.",
            provider.LastHealthStatus ?? "Not checked",
            provider.LastHealthCheckAtUtc,
            string.IsNullOrWhiteSpace(provider.DefaultModel) ? [] : [provider.DefaultModel]);
    }

    private static string BuildManagedSqliteBootstrapProviderConfigurationJson(
        CanDoItAll.Modules.Workspace.ProviderProfile provider)
    {
        var configuration = string.IsNullOrWhiteSpace(provider.ExtraSettingsJson)
            ? new JsonObject()
            : JsonNode.Parse(provider.ExtraSettingsJson)?.AsObject() ?? new JsonObject();
        configuration["connectorPluginKey"] = provider.ConnectorPluginKey;
        configuration["configSchemaVersion"] = provider.ConfigSchemaVersion;
        configuration["timeoutSeconds"] = provider.TimeoutSeconds;
        if (provider.ApiKeySecretId.HasValue)
        {
            configuration["secretRecordId"] = provider.ApiKeySecretId.Value.ToString("D");
        }
        else
        {
            configuration.Remove("secretRecordId");
        }

        return configuration.ToJsonString();
    }

    private static bool ShouldRedirectManagedSqliteAgent(
        AgentDefinition agent,
        IReadOnlySet<Guid> providerIdsToRedirect)
    {
        return agent.ProviderProfileId.HasValue &&
               providerIdsToRedirect.Contains(agent.ProviderProfileId.Value) &&
               agent.ConfigurationJson.Contains(ManagedSqliteSeedMarker, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class DatabaseSwitchCoordinator(
    IDatabaseProfileRuntimeAccessor profileAccessor,
    IDatabaseProfileService profileService,
    IDatabaseDriverRegistry driverRegistry,
    IDatabaseRuntimeState runtimeState,
    IAppDatabaseBootstrapper bootstrapper,
    ILogger<DatabaseSwitchCoordinator> logger) : IDatabaseSwitchCoordinator
{
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(15);

    public async Task<Result<DatabaseSwitchResult>> SwitchAsync(Guid targetProfileId, CancellationToken cancellationToken = default)
    {
        var currentProfile = profileAccessor.ResolveCurrentProfile();
        if (currentProfile.Profile.Runtime.LockedByRuntimeOverride)
        {
            return Result<DatabaseSwitchResult>.Failure(
                Error.Failure("Runtime override is active. Database switching is disabled."));
        }

        if (currentProfile.Profile.Id == targetProfileId)
        {
            var snapshot = runtimeState.GetSnapshot();
            return Result<DatabaseSwitchResult>.Success(new DatabaseSwitchResult(
                currentProfile.Profile.Id,
                currentProfile.Profile.Id,
                snapshot.Generation,
                Environment.ProcessId));
        }

        ResolvedDatabaseProfile targetProfile;
        try
        {
            targetProfile = profileAccessor.ResolveProfile(targetProfileId);
        }
        catch (Exception ex)
        {
            return Result<DatabaseSwitchResult>.Failure(Error.Failure(ex.Message));
        }

        await using var switchSession = await runtimeState.BeginSwitchAsync(cancellationToken);

        try
        {
            await switchSession.WaitForDrainAsync(DrainTimeout, cancellationToken);
            await driverRegistry.Resolve(targetProfile.Profile.ProviderKind)
                .EnsureDatabaseAsync(targetProfile, cancellationToken);
            await bootstrapper.EnsureProfileReadyAsync(targetProfile, cancellationToken);

            var activationResult = await profileService.ActivateAsync(targetProfileId, cancellationToken);
            if (activationResult.IsFailure)
            {
                return Result<DatabaseSwitchResult>.Failure(activationResult.Errors);
            }

            var notification = switchSession.Complete(targetProfile);
            logger.LogInformation(
                "Switched active database from {PreviousProfileId} to {CurrentProfileId} at generation {Generation}.",
                currentProfile.Profile.Id,
                targetProfile.Profile.Id,
                notification.Generation);

            return Result<DatabaseSwitchResult>.Success(new DatabaseSwitchResult(
                currentProfile.Profile.Id,
                targetProfile.Profile.Id,
                notification.Generation,
                Environment.ProcessId));
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(
                ex,
                "Database switch from {PreviousProfileId} to {TargetProfileId} timed out while waiting for active contexts to drain.",
                currentProfile.Profile.Id,
                targetProfileId);

            return Result<DatabaseSwitchResult>.Failure(
                Error.Failure("Database switch timed out while waiting for active operations to finish."));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Database switch from {PreviousProfileId} to {TargetProfileId} failed.",
                currentProfile.Profile.Id,
                targetProfileId);

            return Result<DatabaseSwitchResult>.Failure(
                Error.Failure($"Database switch failed: {ex.Message}"));
        }
    }
}
