using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public sealed class ProjectStructureAgentAdministrationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ISecretProtector secretProtector,
    ProjectsService projectsService,
    IActivityStream activityStream)
{
    private const string DefaultSettingsFileName = "CanDoItAll.Mcp.ProjectStructure.settings.local.json";
    private static readonly JsonSerializerOptions IndentedJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<ProjectStructureAgentWorkspaceSettingsModel> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await WorkspaceSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var settings = await GetLatestSettingsRecordAsync(dbContext, cancellationToken);
        if (settings is null)
        {
            return NewSettings();
        }

        return MapSettings(settings);
    }

    public async Task SaveSettingsAsync(ProjectStructureAgentWorkspaceSettingsModel model, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateSettings(model);
        if (validationError is not null)
        {
            throw new InvalidOperationException(validationError);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await WorkspaceSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var settings = await GetLatestSettingsRecordAsync(dbContext, cancellationToken);
        if (settings is null)
        {
            settings = new ProjectStructureAgentWorkspaceSettingsRecord();
            await dbContext.Set<ProjectStructureAgentWorkspaceSettingsRecord>().AddAsync(settings, cancellationToken);
        }

        settings.CentralBaseUrl = NormalizeBaseUrl(model.CentralBaseUrl);
        settings.InstallScriptPath = NormalizePathOrDefault(model.InstallScriptPath, @"tools\Install-CanDoItAllProjectStructureMcp.ps1");
        settings.SetupReadmePath = NormalizePathOrDefault(model.SetupReadmePath, @"docs\project-structure-mcp-setup.md");
        settings.DefaultAutoApproveMinutes = NormalizeMinutes(model.DefaultAutoApproveMinutes);
        settings.DefaultApprovalRequiredMinutes = NormalizeMinutes(model.DefaultApprovalRequiredMinutes);
        settings.UpdatedAtUtc = clock.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "workspace",
                "save-project-structure-agent-settings",
                "Updated project-structure MCP workspace settings",
                settings.CentralBaseUrl,
                Route: "/settings"),
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectStructureAgentProfileSummary>> ListProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await WorkspaceSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var profiles = await dbContext.Set<ProjectStructureAgentProfileRecord>()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        var overrideCounts = await dbContext.Set<ProjectStructureAgentProjectOverrideRecord>()
            .GroupBy(item => item.ProfileId)
            .Select(group => new
            {
                group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);

        return profiles
            .Select(profile =>
            {
                var token = SafeDecrypt(profile.AccessTokenCipherText);
                return new ProjectStructureAgentProfileSummary(
                    profile.Id,
                    profile.Name,
                    profile.Description,
                    profile.IsEnabled,
                    profile.CapabilityMask,
                    profile.AutoApproveMinutes,
                    profile.ApprovalRequiredMinutes,
                    profile.RequireApprovalForAllMutations,
                    MaskToken(token),
                    overrideCounts.GetValueOrDefault(profile.Id),
                    profile.UpdatedAtUtc);
            })
            .ToList();
    }

    public async Task<ProjectStructureAgentProfileEditorModel> GetProfileAsync(Guid? id, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (!id.HasValue)
        {
            return NewProfile(settings);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await WorkspaceSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var profile = await dbContext.Set<ProjectStructureAgentProfileRecord>()
            .FirstOrDefaultAsync(item => item.Id == id.Value, cancellationToken);
        if (profile is null)
        {
            return NewProfile(settings);
        }

        var overrides = await dbContext.Set<ProjectStructureAgentProjectOverrideRecord>()
            .Where(item => item.ProfileId == profile.Id)
            .OrderBy(item => item.ProjectName)
            .ToListAsync(cancellationToken);

        return new ProjectStructureAgentProfileEditorModel
        {
            Id = profile.Id,
            Name = profile.Name,
            Description = profile.Description,
            IsEnabled = profile.IsEnabled,
            CapabilityMask = profile.CapabilityMask,
            AutoApproveMinutes = profile.AutoApproveMinutes,
            ApprovalRequiredMinutes = profile.ApprovalRequiredMinutes,
            RequireApprovalForAllMutations = profile.RequireApprovalForAllMutations,
            TokenValue = SafeDecrypt(profile.AccessTokenCipherText),
            GenerateNewToken = false,
            Notes = profile.Notes,
            ProjectOverrides = overrides.Select(MapOverrideEditor).ToList()
        };
    }

    public async Task<Result<Guid>> SaveProfileAsync(ProjectStructureAgentProfileEditorModel model, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateProfile(model);
        if (validationError is not null)
        {
            return Result<Guid>.Failure(Error.Validation(validationError));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await WorkspaceSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var projectChoices = await projectsService.ListAsync(cancellationToken);
        var projectsById = projectChoices.ToDictionary(item => item.Id, item => item.Name);
        var duplicateProjectIds = model.ProjectOverrides
            .Where(item => item.ProjectId != Guid.Empty)
            .GroupBy(item => item.ProjectId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateProjectIds.Count > 0)
        {
            return Result<Guid>.Failure(Error.Validation("Each project override must target a unique project."));
        }

        foreach (var projectOverride in model.ProjectOverrides)
        {
            if (projectOverride.ProjectId == Guid.Empty)
            {
                return Result<Guid>.Failure(Error.Validation("Each project override must select a project."));
            }

            if (!projectsById.ContainsKey(projectOverride.ProjectId))
            {
                return Result<Guid>.Failure(Error.Validation($"Project override '{projectOverride.ProjectId}' does not match an existing project."));
            }
        }

        var profile = model.Id.HasValue
            ? await dbContext.Set<ProjectStructureAgentProfileRecord>().FirstOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;

        if (profile is null)
        {
            profile = new ProjectStructureAgentProfileRecord();
            await dbContext.Set<ProjectStructureAgentProfileRecord>().AddAsync(profile, cancellationToken);
        }

        var token = ResolveToken(model);
        profile.Name = model.Name.Trim();
        profile.Description = model.Description?.Trim() ?? string.Empty;
        profile.IsEnabled = model.IsEnabled;
        profile.CapabilityMask = model.CapabilityMask;
        profile.AutoApproveMinutes = NormalizeMinutes(model.AutoApproveMinutes);
        profile.ApprovalRequiredMinutes = NormalizeMinutes(model.ApprovalRequiredMinutes);
        profile.RequireApprovalForAllMutations = model.RequireApprovalForAllMutations;
        profile.AccessTokenCipherText = secretProtector.Protect(token);
        profile.Notes = model.Notes?.Trim() ?? string.Empty;
        profile.UpdatedAtUtc = clock.GetUtcNow();

        var existingOverrides = await dbContext.Set<ProjectStructureAgentProjectOverrideRecord>()
            .Where(item => item.ProfileId == profile.Id)
            .ToListAsync(cancellationToken);
        var incomingOverrideIds = model.ProjectOverrides
            .Where(item => item.Id.HasValue)
            .Select(item => item.Id!.Value)
            .ToHashSet();

        foreach (var existingOverride in existingOverrides.Where(item => !incomingOverrideIds.Contains(item.Id)))
        {
            dbContext.Remove(existingOverride);
        }

        foreach (var projectOverride in model.ProjectOverrides)
        {
            var overrideRecord = projectOverride.Id.HasValue
                ? existingOverrides.FirstOrDefault(item => item.Id == projectOverride.Id.Value)
                : null;

            if (overrideRecord is null)
            {
                overrideRecord = new ProjectStructureAgentProjectOverrideRecord
                {
                    ProfileId = profile.Id
                };
                await dbContext.Set<ProjectStructureAgentProjectOverrideRecord>().AddAsync(overrideRecord, cancellationToken);
            }

            overrideRecord.ProjectId = projectOverride.ProjectId;
            overrideRecord.ProjectName = projectsById[projectOverride.ProjectId];
            overrideRecord.IsEnabled = projectOverride.IsEnabled;
            overrideRecord.CapabilityMask = projectOverride.CapabilityMask;
            overrideRecord.AutoApproveMinutes = NormalizeMinutes(projectOverride.AutoApproveMinutes);
            overrideRecord.ApprovalRequiredMinutes = NormalizeMinutes(projectOverride.ApprovalRequiredMinutes);
            overrideRecord.RequireApprovalForAllMutations = projectOverride.RequireApprovalForAllMutations;
            overrideRecord.Notes = projectOverride.Notes?.Trim() ?? string.Empty;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "workspace",
                model.Id.HasValue ? "update-project-structure-agent-profile" : "create-project-structure-agent-profile",
                $"{(model.Id.HasValue ? "Updated" : "Created")} project-structure MCP agent profile",
                profile.Name,
                ArtifactKind: "project-structure-agent-profile",
                ArtifactId: profile.Id,
                Route: "/settings"),
            cancellationToken);

        return Result<Guid>.Success(profile.Id);
    }

    public async Task DeleteProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await WorkspaceSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var profile = await dbContext.Set<ProjectStructureAgentProfileRecord>()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null)
        {
            return;
        }

        var overrides = await dbContext.Set<ProjectStructureAgentProjectOverrideRecord>()
            .Where(item => item.ProfileId == id)
            .ToListAsync(cancellationToken);

        dbContext.RemoveRange(overrides);
        dbContext.Remove(profile);
        await dbContext.SaveChangesAsync(cancellationToken);

        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "workspace",
                "delete-project-structure-agent-profile",
                "Deleted project-structure MCP agent profile",
                profile.Name,
                ArtifactKind: "project-structure-agent-profile",
                ArtifactId: profile.Id,
                Route: "/settings"),
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectStructureProjectChoice>> ListProjectChoicesAsync(CancellationToken cancellationToken = default)
    {
        var projects = await projectsService.ListAsync(cancellationToken);
        return projects
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ProjectStructureProjectChoice(item.Id, item.Name))
            .ToList();
    }

    public async Task<ProjectStructureAgentSetupGuide> BuildSetupGuideAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var profile = await GetProfileAsync(profileId, cancellationToken);
        if (!profile.Id.HasValue)
        {
            throw new InvalidOperationException($"Project-structure MCP agent profile '{profileId}' was not found.");
        }

        var baseUrl = NormalizeBaseUrl(settings.CentralBaseUrl);
        var token = profile.TokenValue;
        var settingsJson = JsonSerializer.Serialize(
            new
            {
                Server = new
                {
                    Name = "CanDoItAll.Mcp.ProjectStructure",
                    BaseUrl = baseUrl,
                    AgentToken = token,
                    AgentName = profile.Name,
                    RepositoryRoot = ".",
                    BranchName = string.Empty,
                    TimeoutSeconds = 30
                }
            },
            IndentedJsonOptions);

        var powerShellCommand = string.Join(
            " ",
            [
                "powershell",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                settings.InstallScriptPath,
                "-RepoRoot",
                ".",
                "-ServerBaseUrl",
                QuotePowerShell(baseUrl),
                "-AgentToken",
                QuotePowerShell(token)
            ]);

        var codexConfigSnippet =
            $$"""
            [mcp_servers.candoitall_projectstructure]
            command = "{{@"${workspaceFolder}\.artifacts\mcp-installs\CanDoItAll.Mcp.ProjectStructure\current\CanDoItAll.Mcp.ProjectStructure.exe"}}"
            cwd = "{{@"${workspaceFolder}"}}"
            args = [
              "--settings",
              "{{@"${workspaceFolder}\CanDoItAll.Mcp.ProjectStructure.settings.local.json"}}"
            ]
            startup_timeout_sec = 45
            tool_timeout_sec = 1800
            enabled = true
            """;

        return new ProjectStructureAgentSetupGuide(
            baseUrl,
            DefaultSettingsFileName,
            settingsJson,
            codexConfigSnippet,
            powerShellCommand,
            settings.SetupReadmePath,
            token,
            "Store the generated token only on trusted workstations. Rotate it in settings whenever a workstation is retired or the token leaks.");
    }

    public async Task<ProjectStructureAuthorizationDecision> AuthorizeAsync(
        string? agentToken,
        ProjectStructureAgentCapability requiredCapability,
        Guid? projectId,
        int? estimatedMinutes,
        bool enforceMutationApproval,
        CancellationToken cancellationToken = default)
    {
        var (profile, policy) = await ResolveProfileAndPolicyAsync(agentToken, projectId, cancellationToken);

        if (!policy.IsEnabled)
        {
            throw new ProjectStructureAuthorizationException(
                403,
                "AgentDisabled",
                $"Agent profile '{profile.Name}' is disabled for this scope.",
                new
                {
                    profile.Id,
                    profile.Name,
                    projectId,
                    policy.PolicySource
                });
        }

        if ((policy.CapabilityMask & requiredCapability) != requiredCapability)
        {
            throw new ProjectStructureAuthorizationException(
                403,
                "CapabilityDenied",
                $"Agent profile '{profile.Name}' is not allowed to use capability '{requiredCapability}'.",
                new
                {
                    profile.Id,
                    profile.Name,
                    RequiredCapability = requiredCapability,
                    GrantedCapabilities = policy.CapabilityMask,
                    projectId,
                    policy.PolicySource
                });
        }

        if (enforceMutationApproval)
        {
            if (policy.RequireApprovalForAllMutations)
            {
                throw new ProjectStructureAuthorizationException(
                    403,
                    "ApprovalRequired",
                    $"Agent profile '{profile.Name}' requires approval for every mutation in this scope.",
                    new
                    {
                        profile.Id,
                        profile.Name,
                        projectId,
                        estimatedMinutes,
                        policy.PolicySource
                    });
            }

            if (policy.AutoApproveMinutes > 0 || policy.ApprovalRequiredMinutes > 0)
            {
                if (!estimatedMinutes.HasValue)
                {
                    throw new ProjectStructureAuthorizationException(
                        403,
                        "EstimateRequired",
                        $"Agent profile '{profile.Name}' requires an estimated minute value before this mutation can run.",
                        new
                        {
                            profile.Id,
                            profile.Name,
                            projectId,
                            policy.AutoApproveMinutes,
                            policy.ApprovalRequiredMinutes,
                            policy.PolicySource
                        });
                }

                if (policy.ApprovalRequiredMinutes > 0 && estimatedMinutes.Value > policy.ApprovalRequiredMinutes)
                {
                    throw new ProjectStructureAuthorizationException(
                        403,
                        "ApprovalRequired",
                        $"Estimated work of {estimatedMinutes.Value} minute(s) exceeds the approval threshold for profile '{profile.Name}'.",
                        new
                        {
                            profile.Id,
                            profile.Name,
                            projectId,
                            estimatedMinutes,
                            policy.AutoApproveMinutes,
                            policy.ApprovalRequiredMinutes,
                            policy.PolicySource
                        });
                }
            }
        }

        return new ProjectStructureAuthorizationDecision(profile.Id, profile.Name, policy);
    }

    private async Task<(ProjectStructureAgentProfileRecord Profile, ProjectStructureEffectivePolicy Policy)> ResolveProfileAndPolicyAsync(
        string? agentToken,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(agentToken))
        {
            throw new ProjectStructureAuthorizationException(401, "AgentTokenRequired", "A project-structure agent token is required.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await WorkspaceSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var profiles = await dbContext.Set<ProjectStructureAgentProfileRecord>()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var profile = profiles.FirstOrDefault(item => TokenEquals(agentToken.Trim(), SafeDecrypt(item.AccessTokenCipherText)));
        if (profile is null)
        {
            throw new ProjectStructureAuthorizationException(401, "InvalidAgentToken", "The supplied project-structure agent token is not recognized.");
        }

        ProjectStructureEffectivePolicy policy;
        if (projectId.HasValue)
        {
            var projectOverride = await dbContext.Set<ProjectStructureAgentProjectOverrideRecord>()
                .FirstOrDefaultAsync(
                    item => item.ProfileId == profile.Id && item.ProjectId == projectId.Value,
                    cancellationToken);

            if (projectOverride is not null)
            {
                policy = new ProjectStructureEffectivePolicy(
                    profile.Id,
                    profile.Name,
                    projectId,
                    $"Project override: {projectOverride.ProjectName}",
                    projectOverride.IsEnabled,
                    projectOverride.CapabilityMask,
                    projectOverride.AutoApproveMinutes,
                    projectOverride.ApprovalRequiredMinutes,
                    projectOverride.RequireApprovalForAllMutations);

                return (profile, policy);
            }
        }

        policy = new ProjectStructureEffectivePolicy(
            profile.Id,
            profile.Name,
            projectId,
            "Profile default",
            profile.IsEnabled,
            profile.CapabilityMask,
            profile.AutoApproveMinutes,
            profile.ApprovalRequiredMinutes,
            profile.RequireApprovalForAllMutations);

        return (profile, policy);
    }

    private static async Task<ProjectStructureAgentWorkspaceSettingsRecord?> GetLatestSettingsRecordAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var settings = await dbContext.Set<ProjectStructureAgentWorkspaceSettingsRecord>()
            .ToListAsync(cancellationToken);

        return settings
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();
    }

    private static ProjectStructureAgentWorkspaceSettingsModel MapSettings(ProjectStructureAgentWorkspaceSettingsRecord settings)
    {
        return new ProjectStructureAgentWorkspaceSettingsModel
        {
            CentralBaseUrl = settings.CentralBaseUrl,
            InstallScriptPath = settings.InstallScriptPath,
            SetupReadmePath = settings.SetupReadmePath,
            DefaultAutoApproveMinutes = settings.DefaultAutoApproveMinutes,
            DefaultApprovalRequiredMinutes = settings.DefaultApprovalRequiredMinutes
        };
    }

    private static ProjectStructureAgentWorkspaceSettingsModel NewSettings()
    {
        return new ProjectStructureAgentWorkspaceSettingsModel();
    }

    private static ProjectStructureAgentProfileEditorModel NewProfile(ProjectStructureAgentWorkspaceSettingsModel settings)
    {
        return new ProjectStructureAgentProfileEditorModel
        {
            IsEnabled = true,
            CapabilityMask = ProjectStructureAgentCapability.All,
            AutoApproveMinutes = settings.DefaultAutoApproveMinutes,
            ApprovalRequiredMinutes = settings.DefaultApprovalRequiredMinutes,
            GenerateNewToken = true
        };
    }

    private static ProjectStructureAgentProjectOverrideEditorModel MapOverrideEditor(ProjectStructureAgentProjectOverrideRecord projectOverride)
    {
        return new ProjectStructureAgentProjectOverrideEditorModel
        {
            Id = projectOverride.Id,
            ProjectId = projectOverride.ProjectId,
            ProjectName = projectOverride.ProjectName,
            IsEnabled = projectOverride.IsEnabled,
            CapabilityMask = projectOverride.CapabilityMask,
            AutoApproveMinutes = projectOverride.AutoApproveMinutes,
            ApprovalRequiredMinutes = projectOverride.ApprovalRequiredMinutes,
            RequireApprovalForAllMutations = projectOverride.RequireApprovalForAllMutations,
            Notes = projectOverride.Notes
        };
    }

    private static string? ValidateSettings(ProjectStructureAgentWorkspaceSettingsModel model)
    {
        if (string.IsNullOrWhiteSpace(model.CentralBaseUrl))
        {
            return "Central base URL is required.";
        }

        if (!Uri.TryCreate(model.CentralBaseUrl.Trim(), UriKind.Absolute, out _))
        {
            return "Central base URL must be an absolute URL.";
        }

        if (NormalizeMinutes(model.DefaultAutoApproveMinutes) > 0 &&
            NormalizeMinutes(model.DefaultApprovalRequiredMinutes) > 0 &&
            model.DefaultAutoApproveMinutes > model.DefaultApprovalRequiredMinutes)
        {
            return "Default auto-approve minutes cannot exceed the approval-required threshold.";
        }

        return null;
    }

    private static string? ValidateProfile(ProjectStructureAgentProfileEditorModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return "Agent profile name is required.";
        }

        if (model.CapabilityMask == ProjectStructureAgentCapability.None)
        {
            return "At least one capability must be enabled.";
        }

        if (NormalizeMinutes(model.AutoApproveMinutes) > 0 &&
            NormalizeMinutes(model.ApprovalRequiredMinutes) > 0 &&
            model.AutoApproveMinutes > model.ApprovalRequiredMinutes)
        {
            return "Auto-approve minutes cannot exceed the approval-required threshold.";
        }

        foreach (var projectOverride in model.ProjectOverrides)
        {
            if (projectOverride.CapabilityMask == ProjectStructureAgentCapability.None)
            {
                return "Project overrides must keep at least one capability enabled.";
            }

            if (NormalizeMinutes(projectOverride.AutoApproveMinutes) > 0 &&
                NormalizeMinutes(projectOverride.ApprovalRequiredMinutes) > 0 &&
                projectOverride.AutoApproveMinutes > projectOverride.ApprovalRequiredMinutes)
            {
                return $"Project override '{projectOverride.ProjectName}' has an invalid approval range.";
            }
        }

        return null;
    }

    private static string NormalizeBaseUrl(string value)
    {
        return value.Trim().TrimEnd('/');
    }

    private static string NormalizePathOrDefault(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim();
    }

    private static int NormalizeMinutes(int value)
    {
        return Math.Clamp(value, 0, 24 * 60);
    }

    private static string ResolveToken(ProjectStructureAgentProfileEditorModel model)
    {
        if (model.GenerateNewToken || string.IsNullOrWhiteSpace(model.TokenValue))
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        }

        return model.TokenValue.Trim();
    }

    private string SafeDecrypt(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return string.Empty;
        }

        try
        {
            return secretProtector.Unprotect(cipherText);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string MaskToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return "Not generated";
        }

        if (token.Length <= 8)
        {
            return new string('*', token.Length);
        }

        return $"{token[..4]}...{token[^4..]}";
    }

    private static bool TokenEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        if (leftBytes.Length != rightBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string QuotePowerShell(string value)
    {
        return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }
}
