using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Modules.Processes;

internal sealed class StandardProcessLaunchDriverCatalogProvider(
    IProcessExecutionAdapter executionAdapter) : IProcessLaunchDriverCatalogProvider
{
    public ValueTask<ProcessLaunchDriverCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(new ProcessLaunchDriverCatalog(
            new ProcessDriverCatalog(StandardProcessAdapterDriverPackageFactory.CreateLayeredPackages(executionAdapter)),
            StandardProcessAdapterDescriptors.WorkflowAdapter.Strategy.StrategyId,
            StandardProcessAdapterDescriptors.WorkflowAdapter.CapabilityTags));
    }
}

internal sealed class StandardProcessRuntimeStrategyFactoryResolver(
    IProcessExecutionAdapter executionAdapter) : IProcessRuntimeStrategyFactoryResolver
{
    public ValueTask<IProcessStrategyFactory> ResolveAsync(
        ProcessStrategyBindingSnapshot binding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        cancellationToken.ThrowIfCancellationRequested();

        if (binding.StrategyId != executionAdapter.Descriptor.Strategy.StrategyId)
        {
            throw new InvalidOperationException(
                $"No process strategy factory is registered for strategy '{binding.StrategyId}'.");
        }

        return ValueTask.FromResult<IProcessStrategyFactory>(new StandardProcessAdapterStrategyFactory(executionAdapter));
    }
}

internal sealed class AgentFrameworkProcessLaunchExecutorResolver(
    ICanDoItAllAgentWorkspaceFactory workspaceFactory,
    ProcessMockAgentCatalogService processMockAgentCatalogService,
    IProviderProfileService providerProfileService) : IProcessLaunchExecutorResolver
{
    public async ValueTask<ProcessLaunchExecutorResolution> ResolveAsync(
        ProcessLaunchExecutorResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await processMockAgentCatalogService.EnsureCatalogAsync(cancellationToken).ConfigureAwait(false);

        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken).ConfigureAwait(false);
        var providers = await workspaceService.ListProvidersAsync(cancellationToken).ConfigureAwait(false);
        var providerById = providers.ToDictionary(provider => provider.Id);
        var templateStepByKey = request.Definition.Steps.ToDictionary(step => step.Key, StringComparer.OrdinalIgnoreCase);
        var roleByKey = request.Definition.RoleUsages.ToDictionary(role => role.Key, StringComparer.OrdinalIgnoreCase);
        var profileAssignmentByStep = request.LiveRunProfile?.Assignments
            .ToDictionary(assignment => assignment.StepKey, StringComparer.OrdinalIgnoreCase) ??
            new Dictionary<string, ProcessTemplateLiveRunAssignmentDocument>(StringComparer.OrdinalIgnoreCase);
        var overrideByStepKey = request.ExecutorOverrides
            .Where(item => !string.IsNullOrWhiteSpace(item.StepKey))
            .GroupBy(item => item.StepKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last(),
                StringComparer.OrdinalIgnoreCase);
        var bindings = new List<ProcessLaunchExecutorBinding>();
        var findings = new List<ProcessLaunchReadinessFinding>();

        foreach (var planStep in request.Plan.Steps.Where(step => step.IsExecutable))
        {
            if (!templateStepByKey.TryGetValue(planStep.StepKey, out var templateStep))
            {
                findings.Add(new ProcessLaunchReadinessFinding(
                    ProcessLaunchReadinessSeverity.Error,
                    "process.launch.step_template_missing",
                    $"Step '{planStep.StepKey}' has no source template step.",
                    planStep.StepKey));
                continue;
            }

            profileAssignmentByStep.TryGetValue(planStep.StepKey, out var profileAssignment);
            overrideByStepKey.TryGetValue(planStep.StepKey, out var executorOverride);
            var roleKey = ResolveRoleKey(templateStep, profileAssignment);
            var role = roleByKey.GetValueOrDefault(roleKey);
            var roleQuery = ResolveRoleQuery(roleKey, role);
            var requestedExecutorKind = ResolveExecutorKind(profileAssignment, role, executorOverride);
            if (!ProcessLaunchExecutorKinds.CanResolveAsAgent(requestedExecutorKind))
            {
                findings.Add(new ProcessLaunchReadinessFinding(
                    ProcessLaunchReadinessSeverity.Error,
                    "process.launch.executor_kind_unsupported",
                    $"Step '{planStep.StepKey}' role '{roleKey}' requested unsupported executor kind '{requestedExecutorKind}'.",
                    planStep.StepKey,
                    roleKey));
                continue;
            }

            var readinessRequest = CreateReadinessRequest(planStep.StepKey, templateStep, roleKey, role);
            var candidate = executorOverride is null
                ? SelectAgent(readinessRequest, agents, providerById, providerProfileService)
                : ResolveOverrideAgent(executorOverride, agents, providerById, providerProfileService, findings, planStep.StepKey, roleKey);
            if (candidate is null)
            {
                if (executorOverride is null)
                {
                    findings.Add(new ProcessLaunchReadinessFinding(
                        ProcessLaunchReadinessSeverity.Error,
                        "process.launch.agent_missing",
                        FormatMissingAgentMessage(roleQuery, planStep.StepKey),
                        planStep.StepKey,
                        roleKey));
                }

                continue;
            }

            var readiness = AgentProcessReadinessEvaluator.Evaluate(candidate.Agent, readinessRequest);
            if (!readiness.IsExecutionReady)
            {
                AddReadinessFindings(findings, readiness, planStep.StepKey, roleKey);
                continue;
            }

            bindings.Add(new ProcessLaunchExecutorBinding(
                planStep.StepKey,
                roleKey,
                ProcessLaunchExecutorKinds.Agent,
                candidate.Agent.Id.ToString("D"),
                candidate.Agent.Name,
                readiness.ReadinessHash,
                ResolveAssignmentReason(profileAssignment, executorOverride, candidate, roleQuery, requestedExecutorKind)));
        }

        if (findings.Count == 0)
        {
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Info,
                "process.launch.readiness_ok",
                "All executable steps have active agent bindings with enabled structured-output-capable providers."));
        }

        return new ProcessLaunchExecutorResolution(bindings, findings);
    }

    private static string ResolveRoleKey(
        ProcessTemplateDefinitionStepDocument step,
        ProcessTemplateLiveRunAssignmentDocument? assignment)
    {
        if (!string.IsNullOrWhiteSpace(assignment?.RoleKey))
        {
            return assignment.RoleKey.Trim();
        }

        return step.RoleAssignments
            .OrderBy(role => role.FallbackOrder)
            .Select(role => role.RoleKey)
            .FirstOrDefault(roleKey => !string.IsNullOrWhiteSpace(roleKey)) ?? string.Empty;
    }

    private static string ResolveExecutorKind(
        ProcessTemplateLiveRunAssignmentDocument? assignment,
        ProcessTemplateDefinitionRoleUsageDocument? role,
        ProcessLaunchExecutorOverride? executorOverride)
    {
        if (!string.IsNullOrWhiteSpace(executorOverride?.ExecutorKind))
        {
            return executorOverride.ExecutorKind.Trim();
        }

        if (!string.IsNullOrWhiteSpace(assignment?.ExecutorKind))
        {
            return assignment.ExecutorKind.Trim();
        }

        if (!string.IsNullOrWhiteSpace(role?.PreferredExecutorKind))
        {
            return role.PreferredExecutorKind.Trim();
        }

        return ProcessLaunchExecutorKinds.Agent;
    }

    private static ProcessRoleQuery ResolveRoleQuery(
        string roleKey,
        ProcessTemplateDefinitionRoleUsageDocument? role)
    {
        var matchKeys = new[]
            {
                roleKey,
                role?.RoleResourceKey
            }
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ProcessRoleQuery(roleKey, matchKeys.Length == 0 ? [roleKey] : matchKeys);
    }

    private static string FormatMissingAgentMessage(
        ProcessRoleQuery roleQuery,
        string stepKey)
    {
        var aliases = roleQuery.MatchKeys
            .Where(key => !string.Equals(key, roleQuery.BindingRoleKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var aliasSummary = aliases.Length == 0
            ? string.Empty
            : $" Shared role aliases checked: {string.Join(", ", aliases)}.";

        return $"No active agent with an enabled structured-output-capable provider is available for role '{roleQuery.BindingRoleKey}' on step '{stepKey}'.{aliasSummary}";
    }

    private static string ResolveAssignmentReason(
        ProcessTemplateLiveRunAssignmentDocument? profileAssignment,
        ProcessLaunchExecutorOverride? executorOverride,
        AgentProviderCandidate candidate,
        ProcessRoleQuery roleQuery,
        string requestedExecutorKind)
    {
        if (!string.IsNullOrWhiteSpace(executorOverride?.AssignmentReason))
        {
            return $"{executorOverride.AssignmentReason.Trim()} {candidate.ReadinessSummary}";
        }

        if (!string.IsNullOrWhiteSpace(profileAssignment?.BindingReason))
        {
            return $"{profileAssignment.BindingReason.Trim()} {candidate.ReadinessSummary}";
        }

        if (string.Equals(requestedExecutorKind, ProcessLaunchExecutorKinds.Agent, StringComparison.OrdinalIgnoreCase))
        {
            return $"Resolved active agent '{candidate.Agent.Name}' by role '{roleQuery.BindingRoleKey}' using {candidate.MatchSummary}. {candidate.ReadinessSummary}";
        }

        return $"Resolved active agent '{candidate.Agent.Name}' by hybrid executor intent '{requestedExecutorKind}' for role '{roleQuery.BindingRoleKey}' using {candidate.MatchSummary}. {candidate.ReadinessSummary}";
    }

    private static AgentProviderCandidate? SelectAgent(
        AgentProcessRoleReadinessRequest readinessRequest,
        IReadOnlyList<AgentDefinition> agents,
        IReadOnlyDictionary<Guid, ProviderProfile> providerById,
        IProviderProfileService providerProfileService)
    {
        var matches = new List<AgentRoleMatch>();

        foreach (var agent in agents
            .Where(agent => !agent.IsTemplate && agent.Status == AgentLifecycleStatus.Active)
            .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (agent.ProviderProfileId is not { } providerId ||
                !providerById.TryGetValue(providerId, out var provider) ||
                !provider.IsEnabled ||
                !providerProfileService.ResolveFeatureMatrix(provider).SupportsStructuredOutput)
            {
                continue;
            }

            var readiness = AgentProcessReadinessEvaluator.Evaluate(agent, readinessRequest);
            if (readiness.IsExecutionReady && readiness.HasRoleFit)
            {
                matches.Add(new AgentRoleMatch(
                    agent,
                    provider,
                    readiness.Score,
                    readiness.MatchSummary,
                    readiness.ReadinessHash,
                    readiness.ReadinessSummary));
            }
        }

        var bestMatch = matches
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Agent.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return bestMatch is null
            ? null
            : new AgentProviderCandidate(
                bestMatch.Agent,
                bestMatch.Provider,
                bestMatch.Score,
                bestMatch.Summary,
                bestMatch.ReadinessHash,
                bestMatch.ReadinessSummary);
    }

    private static AgentProviderCandidate? ResolveOverrideAgent(
        ProcessLaunchExecutorOverride executorOverride,
        IReadOnlyList<AgentDefinition> agents,
        IReadOnlyDictionary<Guid, ProviderProfile> providerById,
        IProviderProfileService providerProfileService,
        List<ProcessLaunchReadinessFinding> findings,
        string stepKey,
        string roleKey)
    {
        if (!Guid.TryParse(executorOverride.ExecutorId, out var agentId) || agentId == Guid.Empty)
        {
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Error,
                "process.launch.override_agent_invalid",
                $"Step '{stepKey}' role '{roleKey}' selected invalid agent id '{executorOverride.ExecutorId}'.",
                stepKey,
                roleKey));
            return null;
        }

        var agent = agents.FirstOrDefault(candidate => candidate.Id == agentId);
        if (agent is null || agent.IsTemplate || agent.Status != AgentLifecycleStatus.Active)
        {
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Error,
                "process.launch.override_agent_unavailable",
                $"Step '{stepKey}' role '{roleKey}' selected unavailable agent '{executorOverride.ExecutorDisplayName}'.",
                stepKey,
                roleKey));
            return null;
        }

        if (agent.ProviderProfileId is not { } providerId ||
            !providerById.TryGetValue(providerId, out var provider) ||
            !provider.IsEnabled ||
            !providerProfileService.ResolveFeatureMatrix(provider).SupportsStructuredOutput)
        {
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Error,
                "process.launch.override_provider_unavailable",
                $"Step '{stepKey}' role '{roleKey}' selected agent '{agent.Name}' without an enabled structured-output-capable provider.",
                stepKey,
                roleKey));
            return null;
        }

        return new AgentProviderCandidate(
            agent,
            provider,
            0,
            "manual launch review selection",
            string.Empty,
            string.Empty);
    }

    private static AgentProcessRoleReadinessRequest CreateReadinessRequest(
        string stepKey,
        ProcessTemplateDefinitionStepDocument templateStep,
        string roleKey,
        ProcessTemplateDefinitionRoleUsageDocument? role)
    {
        return new AgentProcessRoleReadinessRequest(
            stepKey,
            templateStep.Title,
            roleKey,
            role?.RoleResourceKey ?? string.Empty,
            role?.DisplayName ?? roleKey,
            NormalizeOperations(templateStep.AllowedOperations),
            NormalizeOptional(templateStep.OperationTargetScope));
    }

    private static void AddReadinessFindings(
        List<ProcessLaunchReadinessFinding> findings,
        AgentProcessRoleReadinessResult readiness,
        string stepKey,
        string roleKey)
    {
        foreach (var finding in readiness.Findings.Where(finding => finding.Severity == AgentProcessReadinessFindingSeverity.Error))
        {
            findings.Add(new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Error,
                finding.Code,
                finding.Message,
                stepKey,
                roleKey));
        }
    }

    private static IReadOnlyList<string> NormalizeOperations(IEnumerable<string> operations)
    {
        return operations
            .Where(operation => !string.IsNullOrWhiteSpace(operation))
            .Select(operation => operation.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(operation => operation, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static ExactRoleMatch CalculateBestExactRoleMatch(
        AgentDefinition agent,
        IReadOnlyList<string> matchKeys)
    {
        var bestMatch = ExactRoleMatch.NoMatch;
        foreach (var matchKey in matchKeys)
        {
            var exactScore = CalculateExactRoleScore(
                agent,
                matchKey,
                ProcessMockAgentCatalog.CreateRoleTag(matchKey));
            if (exactScore > bestMatch.Score)
            {
                bestMatch = new ExactRoleMatch(matchKey, exactScore);
            }
        }

        return bestMatch;
    }

    private static int CalculateExactRoleScore(
        AgentDefinition agent,
        string roleKey,
        string roleTag)
    {
        var score = 0;
        if (agent.Tags.Contains(roleTag, StringComparer.OrdinalIgnoreCase))
        {
            score += 30;
        }

        if (agent.Tags.Contains(roleKey, StringComparer.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (string.Equals(ProcessMockAgentCatalog.ResolveRoleKey(agent), roleKey, StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        if (ContainsNormalized(agent.RoleTitle, roleKey) || ContainsNormalized(agent.Name, roleKey))
        {
            score += 10;
        }

        return score;
    }

    private static AgentSemanticRoleMatch CalculateSemanticRoleMatch(
        AgentDefinition agent,
        IReadOnlyList<string> roleTokens)
    {
        if (roleTokens.Count == 0)
        {
            return AgentSemanticRoleMatch.NoMatch;
        }

        var terms = CollectAgentTerms(agent);
        var primaryTerms = CollectPrimaryAgentTerms(agent);
        if (!HasRequiredRoleFamilySignal(roleTokens, terms))
        {
            return AgentSemanticRoleMatch.NoMatch;
        }

        var matchedTokens = new List<string>();
        var score = 0;
        foreach (var roleToken in roleTokens)
        {
            if (!TokenMatches(terms, roleToken))
            {
                continue;
            }

            matchedTokens.Add(roleToken);
            score += ScoreRoleToken(roleToken);
            if (TokenMatches(primaryTerms, roleToken))
            {
                score += PrimaryMetadataMatchBonus;
            }
        }

        score += ScoreWorkloadFit(agent.Workload, roleTokens);
        if (score < SemanticRoleMatchMinimumScore || matchedTokens.Count == 0)
        {
            return AgentSemanticRoleMatch.NoMatch;
        }

        return new AgentSemanticRoleMatch(
            score,
            $"semantic role match on {string.Join(", ", matchedTokens)}");
    }

    private static HashSet<string> CollectAgentTerms(AgentDefinition agent)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddTerms(terms, agent.Name);
        AddTerms(terms, agent.RoleTitle);
        AddTerms(terms, agent.Summary);
        AddTerms(terms, agent.Workload.ToString());

        foreach (var tag in agent.Tags)
        {
            AddTerms(terms, tag);
            var normalizedTag = Normalize(tag);
            if (!string.IsNullOrWhiteSpace(normalizedTag))
            {
                terms.Add(normalizedTag);
            }
        }

        foreach (var capability in agent.Capabilities)
        {
            AddTerms(terms, capability.CapabilityKey);
        }

        return terms;
    }

    private static HashSet<string> CollectPrimaryAgentTerms(AgentDefinition agent)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddTerms(terms, agent.Name);
        AddTerms(terms, agent.RoleTitle);
        return terms;
    }

    private static void AddTerms(HashSet<string> terms, string value)
    {
        foreach (var token in ExtractTokens(value))
        {
            terms.Add(token);
        }
    }

    private static bool HasRequiredRoleFamilySignal(
        IReadOnlyList<string> roleTokens,
        IReadOnlySet<string> terms)
    {
        var hasRoleFamilyRequirement = false;

        if (roleTokens.Any(IsArchitectureRoleToken))
        {
            hasRoleFamilyRequirement = true;
            if (!MatchesAnyTerm(terms, ArchitectureRoleAliases))
            {
                return false;
            }
        }

        if (roleTokens.Any(IsEngineeringRoleToken))
        {
            hasRoleFamilyRequirement = true;
            if (!MatchesAnyTerm(terms, EngineeringRoleAliases))
            {
                return false;
            }
        }

        if (roleTokens.Any(IsQualityRoleToken))
        {
            hasRoleFamilyRequirement = true;
            if (!MatchesAnyTerm(terms, QualityRoleAliases))
            {
                return false;
            }
        }

        if (roleTokens.Any(IsDeliveryRoleToken))
        {
            hasRoleFamilyRequirement = true;
            if (!MatchesAnyTerm(terms, DeliveryRoleAliases))
            {
                return false;
            }
        }

        return hasRoleFamilyRequirement ||
            roleTokens.Any(roleToken => TokenMatches(terms, roleToken));
    }

    private static bool TokenMatches(
        IReadOnlySet<string> terms,
        string roleToken)
    {
        foreach (var alias in ExpandRoleTokenAliases(roleToken))
        {
            if (terms.Contains(alias))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesAnyTerm(
        IReadOnlySet<string> terms,
        IReadOnlyList<string> aliases)
        => aliases.Any(terms.Contains);

    private static int ScoreRoleToken(string roleToken)
    {
        if (TechnologyRoleTokens.Contains(roleToken))
        {
            return 4;
        }

        if (SecondaryRoleTokens.Contains(roleToken))
        {
            return 3;
        }

        return 6;
    }

    private static int ScoreWorkloadFit(
        AgentWorkloadKind workload,
        IReadOnlyList<string> roleTokens)
    {
        if (roleTokens.Any(IsEngineeringRoleToken) && workload == AgentWorkloadKind.Programming)
        {
            return 5;
        }

        if (roleTokens.Any(IsQualityRoleToken) && workload == AgentWorkloadKind.Qa)
        {
            return 5;
        }

        if (roleTokens.Any(IsDeliveryRoleToken) && workload == AgentWorkloadKind.Management)
        {
            return 5;
        }

        return 0;
    }

    private static IReadOnlyList<string> ExpandRoleTokenAliases(string roleToken)
        => roleToken switch
        {
            "architect" or "architecture" or "solution" => ArchitectureRoleAliases,
            "engineer" or "developer" or "implementation" or "programming" => EngineeringRoleAliases,
            "qa" or "quality" or "test" or "tester" or "validation" or "validate" => QualityRoleAliases,
            "delivery" or "release" => DeliveryTokenAliases,
            "manager" => ManagerTokenAliases,
            "product" => ProductRoleAliases,
            "owner" => OwnerRoleAliases,
            "lead" => LeadRoleAliases,
            "blazor" => BlazorRoleAliases,
            "dotnet" or "net" => DotNetRoleAliases,
            "pwa" or "wasm" => PwaRoleAliases,
            _ => [roleToken]
        };

    private static bool IsArchitectureRoleToken(string token)
        => ArchitectureTriggerTokens.Contains(token);

    private static bool IsEngineeringRoleToken(string token)
        => EngineeringTriggerTokens.Contains(token);

    private static bool IsQualityRoleToken(string token)
        => QualityTriggerTokens.Contains(token);

    private static bool IsDeliveryRoleToken(string token)
        => DeliveryTriggerTokens.Contains(token);

    private static IReadOnlyList<string> ExtractTokens(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var tokens = new List<string>();
        var builder = new StringBuilder();
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            AddBuiltToken(tokens, builder);
        }

        AddBuiltToken(tokens, builder);
        return tokens
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddBuiltToken(
        List<string> tokens,
        StringBuilder builder)
    {
        if (builder.Length == 0)
        {
            return;
        }

        tokens.Add(builder.ToString());
        builder.Clear();
    }

    private static bool ContainsNormalized(
        string value,
        string token)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var normalizedValue = Normalize(value);
        var normalizedToken = Normalize(token);
        return normalizedValue.Contains(normalizedToken, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
        => new(value.Where(character => char.IsLetterOrDigit(character)).ToArray());

    private static string ComputeHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private const int SemanticRoleMatchMinimumScore = 6;
    private const int PrimaryMetadataMatchBonus = 3;

    private static readonly HashSet<string> IgnoredRoleTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "add",
        "agent",
        "an",
        "and",
        "application",
        "app",
        "bounded",
        "change",
        "code",
        "create",
        "focused",
        "feature",
        "function",
        "in",
        "of",
        "or",
        "project",
        "process",
        "role",
        "runtime",
        "solution",
        "step",
        "subprocess",
        "the",
        "through",
        "to",
        "with"
    };

    private static readonly HashSet<string> TechnologyRoleTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "blazor",
        "dotnet",
        "net",
        "pwa",
        "wasm"
    };

    private static readonly HashSet<string> SecondaryRoleTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "lead"
    };

    private static readonly HashSet<string> ArchitectureTriggerTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "architect",
        "architecture"
    };

    private static readonly HashSet<string> EngineeringTriggerTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "developer",
        "engineer",
        "implement",
        "implementation",
        "programming",
        "scaffold"
    };

    private static readonly HashSet<string> QualityTriggerTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "qa",
        "quality",
        "test",
        "tester",
        "validate",
        "validation"
    };

    private static readonly HashSet<string> DeliveryTriggerTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "delivery",
        "manager",
        "release"
    };

    private static readonly string[] ArchitectureRoleAliases =
    [
        "architect",
        "architecture",
        "solution",
        "design"
    ];

    private static readonly string[] EngineeringRoleAliases =
    [
        "application",
        "coder",
        "developer",
        "engineer",
        "frontend",
        "fullstack",
        "implementation",
        "programmer",
        "programming",
        "software"
    ];

    private static readonly string[] QualityRoleAliases =
    [
        "browser",
        "qa",
        "quality",
        "review",
        "reviewer",
        "test",
        "tester",
        "validate",
        "validation"
    ];

    private static readonly string[] DeliveryRoleAliases =
    [
        "coordination",
        "coordinator",
        "delivery",
        "evidence",
        "governance",
        "manager",
        "release"
    ];

    private static readonly string[] DeliveryTokenAliases =
    [
        "delivery",
        "evidence",
        "governance",
        "release",
        "writeback"
    ];

    private static readonly string[] ManagerTokenAliases =
    [
        "coordination",
        "coordinator",
        "manager"
    ];

    private static readonly string[] ProductRoleAliases =
    [
        "business",
        "planning",
        "product",
        "requirements",
        "scope",
        "strategy"
    ];

    private static readonly string[] OwnerRoleAliases =
    [
        "business",
        "owner",
        "planning",
        "product",
        "requirements",
        "scope",
        "strategy"
    ];

    private static readonly string[] LeadRoleAliases =
    [
        "lead",
        "manager",
        "review",
        "reviewer"
    ];

    private static readonly string[] BlazorRoleAliases =
    [
        "blazor",
        "frontend",
        "razor",
        "wasm",
        "webassembly"
    ];

    private static readonly string[] DotNetRoleAliases =
    [
        "csharp",
        "dotnet",
        "net"
    ];

    private static readonly string[] PwaRoleAliases =
    [
        "frontend",
        "pwa",
        "wasm",
        "webassembly"
    ];

    private sealed record AgentRoleMatch(
        AgentDefinition Agent,
        ProviderProfile Provider,
        int Score,
        string Summary,
        string ReadinessHash,
        string ReadinessSummary);

    private sealed record ProcessRoleQuery(
        string BindingRoleKey,
        IReadOnlyList<string> MatchKeys);

    private sealed record ExactRoleMatch(
        string MatchKey,
        int Score)
    {
        public static ExactRoleMatch NoMatch { get; } = new(string.Empty, 0);
    }

    private sealed record AgentSemanticRoleMatch(
        int Score,
        string Summary)
    {
        public static AgentSemanticRoleMatch NoMatch { get; } = new(0, string.Empty);
    }

    private sealed record AgentProviderCandidate(
        AgentDefinition Agent,
        ProviderProfile Provider,
        int MatchScore,
        string MatchSummary,
        string ReadinessHash,
        string ReadinessSummary);
}

internal sealed class AgentFrameworkProcessStepBriefBuilder : IProcessStepBriefBuilder
{
    private const string SubprocessLaunchToolName = "project_structure_process_subprocess_launch";
    private readonly GenericProcessStepBriefBuilder genericBuilder = new();

    public string Build(ProcessStepBriefBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var genericBrief = genericBuilder.Build(request);
        var subprocessGuidance = BuildSubprocessGuidance(request.Step);

        return $"""
        {genericBrief}

        AgentFramework execution contract:
        Return only JSON matching the process_step_outcome_result structured output contract.
        Use Status Completed when the step is done, Blocked when required input or tools are missing, Failed for unrecoverable execution failure, or WaitingApproval when a human approval is required.
        If branch outcomes are listed, set BranchOutcomeKey to exactly one listed outcome key.

        Project-scoped launch context:
        Project id: {request.LaunchRequest.ProjectId?.ToString("D") ?? "not scoped"}
        Project node id: {request.LaunchRequest.ProjectNodeId ?? "not scoped"}

        AgentFramework evidence write rule:
        Write process step summaries, proof, screenshots, logs, and handoff notes under the managed artifact root or a child path. Managed artifact refs are workspace-managed relative paths; use them exactly as shown and never convert them to external-target paths. Include the written managed artifact paths in evidenceRefs. Do not write evidence under output/ unless this step is explicitly mutating a managed product output path.

        AgentFramework subprocess adapter guidance:
        {subprocessGuidance}
        """;
    }

    private static string BuildSubprocessGuidance(ProcessTemplateDefinitionStepDocument step)
    {
        var isSubprocessStep = string.Equals(step.StepKind, ProcessTemplateStepKinds.Subprocess, StringComparison.OrdinalIgnoreCase) ||
                               !string.IsNullOrWhiteSpace(step.SubprocessProcessKey);
        if (!isSubprocessStep)
        {
            return "No subprocess mapping.";
        }

        var hasSubprocessKey = !string.IsNullOrWhiteSpace(step.SubprocessProcessKey);
        var subprocessKey = hasSubprocessKey
            ? step.SubprocessProcessKey.Trim()
            : "not mapped";
        var snapshotName = string.IsNullOrWhiteSpace(step.SubprocessDefinitionSnapshotName)
            ? "not supplied"
            : step.SubprocessDefinitionSnapshotName.Trim();
        var launchInstruction = !hasSubprocessKey
            ? "This step is marked as a subprocess but has no child process definition key. Return Blocked unless upstream evidence already supplies the missing child run."
            : $"Use {SubprocessLaunchToolName} with DefinitionKey \"{subprocessKey}\" when {ProcessOperationContractNames.ExecuteExternalAction} is allowed. Do not mark Completed until the child run receipt and required child evidence are available, or return Blocked with the missing evidence.";

        return $"""
        - Child process definition key: {subprocessKey}
        - Child definition snapshot name: {snapshotName}
        - Governed launch tool: {SubprocessLaunchToolName}
        - Completion rule: {launchInstruction}
        - Live-run profile rule: leave LiveRunProfileKey empty unless the launch variables explicitly provide a valid process live-run profile key for this child definition. BranchName, RepositoryRoot, SessionId, parent DefinitionKey, and child DefinitionKey are not live-run profile keys.
        - Retry rule: repeated launch-tool calls for the same parent run, parent step, project node, and child definition return the existing child run instead of creating another child.
        - Evidence rule: the launch tool result includes ChildManagedArtifactRoot, ChildStepsArtifactRoot, ChildLiveProcessesRoute, and ExpectedChildEvidenceRefs. Treat artifacts under ChildManagedArtifactRoot as the child evidence bundle; do not require child evidence to be copied into the parent run root.
        """;
    }
}

internal sealed class AgentFrameworkProcessExecutionAdapter(
    ICanDoItAllAgentWorkspaceFactory workspaceFactory,
    IProcessRuntimeStepAssignmentStore assignmentStore,
    IProcessRuntimeStateStore stateStore) : IProcessExecutionAdapter
{
    private static readonly Regex ProcessRunIdPattern = new(
        @"(?<![0-9a-fA-F])[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(?![0-9a-fA-F])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public ProcessExecutionAdapterDescriptor Descriptor => StandardProcessAdapterDescriptors.WorkflowAdapter;

    public async ValueTask<ProcessExecutionAdapterResult> ExecuteAsync(
        ProcessExecutionAdapterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.StepId is not { } stepId)
        {
            return Failed("process.adapter.step_missing", "The process execution adapter requires a concrete step id.", "step-missing");
        }

        var assignment = await assignmentStore.LoadAsync(request.RunId, stepId, cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            return Failed("process.adapter.assignment_missing", $"No runtime assignment exists for step '{stepId}'.", stepId.ToString());
        }

        if (await TryResolveExistingPendingChildRunAsync(
                assignment,
                assignmentStore,
                stateStore,
                cancellationToken).ConfigureAwait(false) is { } existingPendingChildRunId)
        {
            throw new ProcessRuntimeDispatchDeferredException(
                $"Step '{assignment.StepKey}' is waiting for active child process run '{existingPendingChildRunId}'.",
                existingPendingChildRunId);
        }

        if (!string.Equals(assignment.ExecutorKind, ProcessLaunchExecutorKinds.Agent, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(assignment.ExecutorId, out var agentId))
        {
            return Failed(
                "process.adapter.executor_invalid",
                $"Step '{assignment.StepKey}' has invalid executor binding '{assignment.ExecutorKind}:{assignment.ExecutorId}'.",
                assignment.ExecutorId);
        }

        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken).ConfigureAwait(false);
        var agent = agents.FirstOrDefault(candidate => candidate.Id == agentId);
        if (agent is null)
        {
            return NeedsManager(
                "process.adapter.executor_agent_missing",
                $"Step '{assignment.StepKey}' is assigned to missing agent '{agentId}'.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{agentId}");
        }

        var readiness = AgentProcessReadinessEvaluator.Evaluate(agent, CreateRuntimeReadinessRequest(assignment));
        if (!readiness.IsExecutionReady || !readiness.HasRoleFit)
        {
            return NeedsManager(
                "process.adapter.executor_readiness_failed",
                $"Step '{assignment.StepKey}' cannot run with assigned agent '{agent.Name}': {readiness.ReadinessSummary}",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{agentId}:{readiness.ReadinessHash}");
        }

        try
        {
            var metadataJson = BuildProcessExecutionMetadata(assignment);
            var result = await workspaceService
                .ExecuteRunAsync(
                    new ExecutionRunRequest(
                        agentId,
                        assignment.Prompt,
                        Context: new ExecutionInvocationContext(
                            SourceKind: ProcessMockAgentCatalog.ProcessSourceKind,
                            SourceId: assignment.StepKey,
                            CorrelationId: request.RunId.ToString(),
                            CausationId: stepId.ToString(),
                            RequestedBy: "process-runtime",
                            RequestedByKind: "system",
                            MetadataJson: metadataJson,
                            ProcessRunId: request.RunId.ToString(),
                            ProcessStepId: stepId.ToString(),
                            Policy: new ExecutionInvocationPolicy(
                                MaxStructuredOutputRepairAttempts: ExecutionInvocationMetadata.DefaultGovernedRepairAttempts)),
                        AutoApprovePendingToolCalls: true,
                        StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult),
                    cancellationToken)
                .ConfigureAwait(false);
            var validation = AgentOutputJson.DeserializeAndValidate(
                result.ResponseText,
                new ProcessStepOutcomeValidator());

            if (!validation.Succeeded || validation.Output is null)
            {
                return Failed(
                    "process.adapter.output_invalid",
                    FormatValidationErrors(validation.Validation.Errors),
                    validation.RawOutputHash);
            }

            if (await TryResolvePendingChildRunAsync(
                    assignment,
                    validation.Output,
                    stateStore,
                    cancellationToken).ConfigureAwait(false) is { } pendingChildRunId)
            {
                throw new ProcessRuntimeDispatchDeferredException(
                    $"Step '{assignment.StepKey}' is waiting for active child process run '{pendingChildRunId}'.",
                    pendingChildRunId);
            }

            return ToAdapterResult(assignment, validation.Output, validation.RawOutputHash);
        }
        catch (ProcessRuntimeDispatchDeferredException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failed(
                "process.adapter.agent_execution_failed",
                $"Agent execution failed for step '{assignment.StepKey}': {exception.Message}",
                ComputeHash(exception.GetType().FullName + ":" + exception.Message));
        }
    }

    private static string BuildProcessExecutionMetadata(ProcessRuntimeStepAssignment assignment)
    {
        var allowedOperations = NormalizeOperations(assignment.AllowedOperations);
        var targetScope = string.IsNullOrWhiteSpace(assignment.OperationTargetScope)
            ? string.Empty
            : assignment.OperationTargetScope.Trim();
        var allowsProductMutation = AllowsProductMutation(allowedOperations, targetScope);
        var allowsBrowserProof = AllowsBrowserRuntimeProof(allowedOperations);
        var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [ExecutionInvocationMetadata.ProcessBrowserToolsAllowedMetadataKey] = allowsBrowserProof,
            [ExecutionInvocationMetadata.ProcessStepAllowedOperationsMetadataKey] = allowedOperations,
            [ExecutionInvocationMetadata.ProcessStepTargetScopeMetadataKey] = targetScope,
            [ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey] = allowsProductMutation
        };
        var trustedAliases = ResolveTrustedExternalTargetAliases(assignment.LaunchVariables);
        if (trustedAliases.Count > 0 && UsesExternalProductTarget(targetScope))
        {
            metadata[allowsProductMutation
                ? ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey
                : ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey] = trustedAliases;
        }

        var metadataJson = ApplyLaunchContextMetadata(
            JsonSerializer.Serialize(metadata, AgentOutputJson.SerializerOptions),
            assignment.LaunchVariables);
        return ExecutionInvocationMetadata.Build(
            metadataJson,
            new ExecutionInvocationPolicy(
                MaxStructuredOutputRepairAttempts: ExecutionInvocationMetadata.DefaultGovernedRepairAttempts));
    }

    private static string ApplyLaunchContextMetadata(
        string metadataJson,
        IReadOnlyDictionary<string, string> launchVariables)
    {
        metadataJson = ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
            metadataJson,
            ResolveProjectWorkspaceScope(launchVariables));
        return ExecutionInvocationMetadata.ApplyProjectStructureLaunchAgent(
            metadataJson,
            ResolveProjectStructureLaunchAgent(launchVariables));
    }

    private static WorkspaceScopeDescriptor? ResolveProjectWorkspaceScope(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        return TryResolveLaunchGuid(launchVariables, ProcessLaunchVariableNames.ProjectId, out var projectId)
            ? WorkspaceScopeDescriptor.Project(projectId.ToString("D"))
            : null;
    }

    private static ProjectStructureAgentIdentityDescriptor? ResolveProjectStructureLaunchAgent(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        var descriptor = new ProjectStructureAgentIdentityDescriptor(
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.AgentId),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.AgentName),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.MachineName),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.RepositoryRoot),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.BranchName),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.SessionId));
        return descriptor.HasLeaseOwnerIdentity ? descriptor : null;
    }

    private static bool TryResolveLaunchGuid(
        IReadOnlyDictionary<string, string> launchVariables,
        string key,
        out Guid value)
    {
        value = Guid.Empty;
        return launchVariables.TryGetValue(key, out var rawValue) &&
               Guid.TryParse(rawValue, out value) &&
               value != Guid.Empty;
    }

    private static string ResolveLaunchVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        string key)
    {
        return launchVariables.TryGetValue(key, out var value)
            ? value.Trim()
            : string.Empty;
    }

    private static IReadOnlyList<string> NormalizeOperations(IReadOnlyList<string> operations)
    {
        return operations
            .Where(operation => !string.IsNullOrWhiteSpace(operation))
            .Select(operation => operation.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(operation => operation, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool AllowsProductMutation(
        IReadOnlyList<string> allowedOperations,
        string targetScope)
    {
        return allowedOperations.Contains(ProcessOperationContractNames.MutateProductTarget, StringComparer.OrdinalIgnoreCase) ||
            string.Equals(targetScope, ProcessOperationContractNames.ExternalProductTargetMutable, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(targetScope, ProcessOperationContractNames.ManagedOutputProduct, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AllowsBrowserRuntimeProof(IReadOnlyList<string> allowedOperations)
    {
        return allowedOperations.Contains(ProcessOperationContractNames.CaptureRuntimeProof, StringComparer.OrdinalIgnoreCase);
    }

    private static bool UsesExternalProductTarget(string targetScope)
    {
        return string.Equals(targetScope, ProcessOperationContractNames.ExternalProductTargetMutable, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(targetScope, ProcessOperationContractNames.ExternalProductTargetReadOnly, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ResolveTrustedExternalTargetAliases(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        return launchVariables
            .Where(item => TrustedExternalTargetVariableNames.Contains(item.Key))
            .Select(item => AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(item.Value))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Where(item => item.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static readonly HashSet<string> TrustedExternalTargetVariableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ExternalTargetAlias",
        "ExternalTargetRoot",
        "OutputRoot",
        "OutputRootAlias",
        "ProductRoot",
        "ProductRootAlias",
        "WorkspaceAlias"
    };

    internal static async ValueTask<ProcessRunId?> TryResolvePendingChildRunAsync(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IProcessRuntimeStateStore stateStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(stateStore);

        if (output.Status is not (ProcessStepOutcomeStatus.Blocked or ProcessStepOutcomeStatus.WaitingApproval) ||
            !CanWaitOnControlledChildRun(assignment))
        {
            return null;
        }

        var currentState = await stateStore.LoadAsync(assignment.RunId, cancellationToken).ConfigureAwait(false);
        if (currentState is null)
        {
            return null;
        }

        foreach (var candidateRunId in ExtractReferencedRunIds(output))
        {
            var pendingRunId = await TryResolveNonTerminalProcessTreeRunAsync(
                assignment.RunId,
                currentState,
                candidateRunId,
                stateStore,
                cancellationToken).ConfigureAwait(false);
            if (pendingRunId is not null)
            {
                return pendingRunId;
            }
        }

        return null;
    }

    internal static async ValueTask<ProcessRunId?> TryResolveExistingPendingChildRunAsync(
        ProcessRuntimeStepAssignment assignment,
        IProcessRuntimeStepAssignmentStore assignmentStore,
        IProcessRuntimeStateStore stateStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(assignmentStore);
        ArgumentNullException.ThrowIfNull(stateStore);

        if (!CanWaitOnControlledChildRun(assignment))
        {
            return null;
        }

        var currentState = await stateStore.LoadAsync(assignment.RunId, cancellationToken).ConfigureAwait(false);
        if (currentState is null)
        {
            return null;
        }

        var childAssignments = await assignmentStore
            .FindByLaunchVariablesAsync(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ParentProcessRunId"] = assignment.RunId.ToString(),
                    ["ParentProcessStepId"] = assignment.StepInstanceId.ToString()
                },
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var candidateRunId in childAssignments
            .OrderByDescending(childAssignment => childAssignment.CreatedAtUtc)
            .Select(childAssignment => childAssignment.RunId)
            .Distinct())
        {
            var pendingRunId = await TryResolveNonTerminalProcessTreeRunAsync(
                assignment.RunId,
                currentState,
                candidateRunId,
                stateStore,
                cancellationToken).ConfigureAwait(false);
            if (pendingRunId is not null)
            {
                return pendingRunId;
            }
        }

        return null;
    }

    private static async ValueTask<ProcessRunId?> TryResolveNonTerminalProcessTreeRunAsync(
        ProcessRunId currentRunId,
        ProcessRuntimeStateSnapshot currentState,
        ProcessRunId candidateRunId,
        IProcessRuntimeStateStore stateStore,
        CancellationToken cancellationToken)
    {
        if (candidateRunId == currentRunId)
        {
            return null;
        }

        var candidateState = await stateStore.LoadAsync(candidateRunId, cancellationToken).ConfigureAwait(false);
        if (candidateState is null ||
            ProcessRuntimeTerminalStates.IsRunTerminal(candidateState.Status) ||
            !IsSameProcessTree(currentState, candidateState))
        {
            return null;
        }

        return candidateRunId;
    }

    private static bool CanWaitOnControlledChildRun(ProcessRuntimeStepAssignment assignment)
    {
        return assignment.AllowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase) ||
            string.Equals(assignment.OperationTargetScope, ProcessOperationContractNames.ExternalActionControlled, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameProcessTree(
        ProcessRuntimeStateSnapshot currentState,
        ProcessRuntimeStateSnapshot candidateState)
    {
        return candidateState.RootRunId == currentState.RootRunId ||
            candidateState.RootRunId == currentState.RunId ||
            candidateState.RunId == currentState.RootRunId;
    }

    private static IReadOnlyList<ProcessRunId> ExtractReferencedRunIds(ProcessStepOutcomeResult output)
    {
        var runIds = new List<ProcessRunId>();
        foreach (var text in EnumerateOutcomeText(output))
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (Match match in ProcessRunIdPattern.Matches(text))
            {
                if (Guid.TryParse(match.Value, out var runGuid))
                {
                    var runId = new ProcessRunId(runGuid);
                    if (!runIds.Contains(runId))
                    {
                        runIds.Add(runId);
                    }
                }
            }
        }

        return runIds;
    }

    private static IEnumerable<string?> EnumerateOutcomeText(ProcessStepOutcomeResult output)
    {
        yield return output.Reason;
        yield return output.BranchOutcomeKey;
        yield return output.BranchOutcomeTitle;
        yield return output.HumanReadableSummaryMarkdown;

        foreach (var evidenceRef in output.EvidenceRefs)
        {
            yield return evidenceRef;
        }

        foreach (var nextAction in output.NextActions)
        {
            yield return nextAction;
        }
    }

    private static class ProcessLaunchVariableNames
    {
        public const string AgentId = "AgentId";
        public const string AgentName = "AgentName";
        public const string BranchName = "BranchName";
        public const string MachineName = "MachineName";
        public const string ProjectId = "ProjectId";
        public const string RepositoryRoot = "RepositoryRoot";
        public const string SessionId = "SessionId";
    }

    private static ProcessExecutionAdapterResult ToAdapterResult(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string rawOutputHash)
    {
        var outcome = output.Status switch
        {
            ProcessStepOutcomeStatus.Completed => StrategyOutcome.Succeeded,
            ProcessStepOutcomeStatus.Blocked or ProcessStepOutcomeStatus.WaitingApproval => StrategyOutcome.NeedsManager,
            ProcessStepOutcomeStatus.Refused => StrategyOutcome.Canceled,
            ProcessStepOutcomeStatus.Failed => StrategyOutcome.Failed,
            _ => StrategyOutcome.Failed
        };
        if (outcome == StrategyOutcome.Succeeded &&
            ValidateProductMutationCompletion(assignment, output) is { } productMutationIssue)
        {
            var requestedArtifactSlots = assignment.ProducedArtifactSlotIds.Count > 0
                ? assignment.ProducedArtifactSlotIds
                : assignment.RequiredArtifactSlotIds;
            return new ProcessExecutionAdapterResult(
                StrategyOutcome.NeedsManager,
                [],
                requestedArtifactSlots
                    .Select(slotId => new RequestedArtifactRef(
                        slotId,
                        ComputeHash($"{rawOutputHash}:requested:{slotId}:{productMutationIssue.Code}")))
                    .ToArray(),
                [
                    new ProcessExecutionAdapterDiagnostic(
                        new StrategyDiagnosticCode(productMutationIssue.Code),
                        StrategyDiagnosticSensitivity.Normal,
                        ComputeHash(productMutationIssue.Evidence),
                        productMutationIssue.Summary,
                        RestrictedEvidenceReference: null,
                        productMutationIssue.RetrySafety,
                        productMutationIssue.Idempotency)
                ],
                [
                    new ManagerSignal(
                        new ManagerSignalCode(productMutationIssue.Code),
                        ComputeHash($"{rawOutputHash}:manager:{productMutationIssue.Code}:{productMutationIssue.Evidence}"),
                        productMutationIssue.Summary)
                ],
                productMutationIssue.Summary,
                ComputeHash($"{rawOutputHash}:{productMutationIssue.Code}:{productMutationIssue.Evidence}"));
        }

        IReadOnlyList<ProducedArtifactRef> artifacts = outcome == StrategyOutcome.Succeeded
            ? assignment.ProducedArtifactSlotIds
                .Select(slotId => new ProducedArtifactRef(
                    ArtifactInstanceId.New(),
                    slotId,
                    ComputeHash($"{rawOutputHash}:{assignment.StepInstanceId}:{slotId}")))
                .ToArray()
            : [];
        IReadOnlyList<RequestedArtifactRef> requestedArtifacts = outcome == StrategyOutcome.NeedsManager
            ? assignment.RequiredArtifactSlotIds
                .Select(slotId => new RequestedArtifactRef(
                    slotId,
                    ComputeHash($"{rawOutputHash}:requested:{slotId}")))
                .ToArray()
            : [];
        var managerSignals = new List<ManagerSignal>();
        if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
        {
            managerSignals.Add(new ManagerSignal(
                ProcessBranchSignalCodes.Outcome(output.BranchOutcomeKey),
                ComputeHash(output.BranchOutcomeKey),
                string.IsNullOrWhiteSpace(output.BranchOutcomeTitle)
                    ? $"Branch outcome selected: {output.BranchOutcomeKey}"
                    : output.BranchOutcomeTitle));
        }

        return new ProcessExecutionAdapterResult(
            outcome,
            artifacts,
            requestedArtifacts,
            [],
            managerSignals,
            output.Reason,
            rawOutputHash);
    }

    private static ProductMutationCompletionIssue? ValidateProductMutationCompletion(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            !AllowsProductMutation(NormalizeOperations(assignment.AllowedOperations), assignment.OperationTargetScope))
        {
            return null;
        }

        if (output.EvidenceRefs.Count == 0 ||
            output.EvidenceRefs.All(string.IsNullOrWhiteSpace))
        {
            return new ProductMutationCompletionIssue(
                "process.adapter.product_output_evidence_missing",
                $"Step '{assignment.StepKey}' claimed completion for a product-mutating scope but returned no evidence references.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:evidence-missing",
                ProcessDiagnosticRetrySafety.SafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Idempotent);
        }

        if (!TryResolveInspectableProductRoot(assignment.LaunchVariables, out var productRoot))
        {
            return null;
        }

        var inspection = InspectProductRoot(productRoot);
        if (inspection.HasProductFiles)
        {
            return null;
        }

        return new ProductMutationCompletionIssue(
            "process.adapter.product_output_missing",
            inspection.Summary.Length == 0
                ? $"Step '{assignment.StepKey}' claimed completion but the configured product output root '{productRoot}' contains no product files."
                : $"Step '{assignment.StepKey}' claimed completion but the configured product output root '{productRoot}' is not usable: {inspection.Summary}",
            productRoot,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static bool TryResolveInspectableProductRoot(
        IReadOnlyDictionary<string, string> launchVariables,
        out string productRoot)
    {
        productRoot = FirstNonEmpty(
            ResolveLaunchVariable(launchVariables, "OutputRoot"),
            ResolveLaunchVariable(launchVariables, "ProductRoot"),
            ResolveLaunchVariable(launchVariables, "ExternalTargetRoot"));
        if (string.IsNullOrWhiteSpace(productRoot) ||
            productRoot.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase) ||
            !Path.IsPathFullyQualified(productRoot))
        {
            productRoot = string.Empty;
            return false;
        }

        productRoot = Path.GetFullPath(productRoot);
        return true;
    }

    private static ProductRootInspection InspectProductRoot(string productRoot)
    {
        try
        {
            if (!Directory.Exists(productRoot))
            {
                return new ProductRootInspection(false, "the directory does not exist");
            }

            return Directory
                .EnumerateFiles(productRoot, "*", SearchOption.AllDirectories)
                .Any(file => IsProductFile(productRoot, file))
                ? new ProductRootInspection(true, string.Empty)
                : new ProductRootInspection(false, "no product files were found");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or NotSupportedException)
        {
            return new ProductRootInspection(false, exception.Message);
        }
    }

    private static bool IsProductFile(string productRoot, string file)
    {
        var relativePath = Path.GetRelativePath(productRoot, file);
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Any(IsIgnoredProductPathSegment))
        {
            return false;
        }

        var fileName = Path.GetFileName(file);
        return !string.Equals(fileName, ".gitkeep", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(fileName, ".DS_Store", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(fileName, "Thumbs.db", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnoredProductPathSegment(string segment)
        => string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, ".vs", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, "node_modules", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, "packages", StringComparison.OrdinalIgnoreCase);

    private static ProcessExecutionAdapterResult Failed(
        string code,
        string summary,
        string evidence)
    {
        return new ProcessExecutionAdapterResult(
            StrategyOutcome.Failed,
            [],
            [],
            [
                new ProcessExecutionAdapterDiagnostic(
                    new StrategyDiagnosticCode(code),
                    StrategyDiagnosticSensitivity.Normal,
                    ComputeHash(evidence),
                    summary,
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.Unknown,
                    ProcessDiagnosticIdempotencyClassification.Unknown)
            ],
            [],
            summary,
            ComputeHash($"{code}:{evidence}"));
    }

    private static ProcessExecutionAdapterResult NeedsManager(
        string code,
        string summary,
        string evidence)
    {
        var evidenceHash = ComputeHash(evidence);
        return new ProcessExecutionAdapterResult(
            StrategyOutcome.NeedsManager,
            [],
            [],
            [
                new ProcessExecutionAdapterDiagnostic(
                    new StrategyDiagnosticCode(code),
                    StrategyDiagnosticSensitivity.Normal,
                    evidenceHash,
                    summary,
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.UnsafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [
                new ManagerSignal(
                    new ManagerSignalCode(code),
                    evidenceHash,
                    summary)
            ],
            summary,
            ComputeHash($"{code}:{evidence}"));
    }

    private static AgentProcessRoleReadinessRequest CreateRuntimeReadinessRequest(ProcessRuntimeStepAssignment assignment)
    {
        return new AgentProcessRoleReadinessRequest(
            assignment.StepKey,
            assignment.StepKey,
            assignment.RoleKey,
            assignment.RoleResourceKey,
            assignment.RoleDisplayName,
            NormalizeOperations(assignment.AllowedOperations),
            assignment.OperationTargetScope);
    }

    private static string FormatValidationErrors(IReadOnlyList<AgentOutputValidationError> errors)
    {
        if (errors.Count == 0)
        {
            return "Agent output did not satisfy the process step outcome contract.";
        }

        return string.Join("; ", errors.Select(error => $"{error.Code}: {error.Message}"));
    }

    private static string ComputeHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private sealed record ProductMutationCompletionIssue(
        string Code,
        string Summary,
        string Evidence,
        ProcessDiagnosticRetrySafety RetrySafety,
        ProcessDiagnosticIdempotencyClassification Idempotency);

    private sealed record ProductRootInspection(
        bool HasProductFiles,
        string Summary);
}

internal sealed class AgentFrameworkProcessExecutionObservationReader(
    IAgentFrameworkWorkspaceService workspaceService) : IProcessExecutionObservationReader
{
    public async ValueTask<IReadOnlyList<ProcessExecutionObservation>> ListAsync(
        ProcessExecutionObservationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.RunIds.Count == 0)
        {
            return [];
        }

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken).ConfigureAwait(false);
        var agentNameById = agents.ToDictionary(agent => agent.Id, agent => agent.Name);
        var observations = new List<ProcessExecutionObservation>();

        foreach (var runId in query.RunIds.Distinct())
        {
            var executionRuns = await workspaceService.ListExecutionRunsAsync(
                new ExecutionRunQuery(
                    ProcessRunId: runId.ToString(),
                    Take: Math.Max(1, query.TakePerRun),
                    UpdatedFromUtc: query.FromUtc,
                    UpdatedToUtc: query.ToUtc),
                cancellationToken).ConfigureAwait(false);

            foreach (var executionRun in executionRuns)
            {
                if (!TryParseProcessIdentity(executionRun, out var processRunId, out var stepInstanceId))
                {
                    continue;
                }

                ExecutionRunDetail? detail = null;
                try
                {
                    detail = await workspaceService.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                }

                var detailRun = detail?.Run ?? executionRun;
                var agentName = agentNameById.GetValueOrDefault(detailRun.AgentId);
                observations.Add(new ProcessExecutionObservation(
                    detailRun.Id,
                    processRunId,
                    stepInstanceId,
                    detailRun.AgentId,
                    FirstNonEmpty(agentName, detailRun.RequestedBy, detailRun.AgentId.ToString("D")),
                    detailRun.ProviderName,
                    detailRun.Model,
                    detailRun.State.ToString(),
                    detailRun.Outcome?.ToString() ?? string.Empty,
                    detailRun.CreatedAtUtc,
                    detailRun.UpdatedAtUtc,
                    detailRun.StartedAtUtc,
                    detailRun.CompletedAtUtc,
                    detailRun.InputSummary,
                    detailRun.ResultSummary,
                    MapActivities(detail),
                    MapTools(detail),
                    MapArtifacts(detail),
                    ResolveLastError(detail)));
            }
        }

        return observations
            .OrderByDescending(observation => observation.UpdatedAtUtc)
            .ToArray();
    }

    private static IReadOnlyList<ProcessExecutionActivityObservation> MapActivities(ExecutionRunDetail? detail)
        => detail?.ExecutionLog
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Take(8)
            .OrderBy(entry => entry.CreatedAtUtc)
            .Select(entry => new ProcessExecutionActivityObservation(
                entry.CreatedAtUtc,
                entry.State.ToString(),
                entry.Phase,
                entry.Message))
            .ToArray() ?? [];

    private static IReadOnlyList<ProcessExecutionToolObservation> MapTools(ExecutionRunDetail? detail)
        => detail?.ToolReceipts
            .OrderByDescending(tool => tool.CompletedAtUtc)
            .Take(8)
            .OrderBy(tool => tool.StartedAtUtc)
            .Select(tool => new ProcessExecutionToolObservation(
                tool.ToolName,
                tool.RuntimeToolProviderKey,
                tool.RequestSummary,
                tool.ExitSummary,
                tool.StartedAtUtc,
                tool.CompletedAtUtc))
            .ToArray() ?? [];

    private static IReadOnlyList<ProcessExecutionArtifactObservation> MapArtifacts(ExecutionRunDetail? detail)
        => detail?.Artifacts
            .OrderByDescending(artifact => artifact.CreatedAtUtc)
            .Take(8)
            .OrderBy(artifact => artifact.CreatedAtUtc)
            .Select(artifact => new ProcessExecutionArtifactObservation(
                artifact.ArtifactKind,
                artifact.DisplayName,
                artifact.RelativePath,
                artifact.Summary,
                artifact.CreatedAtUtc))
            .ToArray() ?? [];

    private static string ResolveLastError(ExecutionRunDetail? detail)
    {
        if (detail is null)
        {
            return string.Empty;
        }

        return detail.ExecutionLog
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .FirstOrDefault(entry =>
                entry.State == ExecutionState.Failed ||
                entry.Phase.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                entry.Message.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                entry.Message.Contains("exception", StringComparison.OrdinalIgnoreCase))
            ?.Message ?? string.Empty;
    }

    private static bool TryParseProcessIdentity(
        ExecutionRunRecord executionRun,
        out ProcessRunId runId,
        out ProcessStepInstanceId stepInstanceId)
    {
        runId = default;
        stepInstanceId = default;

        return Guid.TryParse(executionRun.ProcessRunId, out var parsedRunId) &&
               parsedRunId != Guid.Empty &&
               Guid.TryParse(executionRun.ProcessStepId, out var parsedStepId) &&
               parsedStepId != Guid.Empty &&
               TryCreateProcessRunId(parsedRunId, out runId) &&
               TryCreateStepInstanceId(parsedStepId, out stepInstanceId);
    }

    private static bool TryCreateProcessRunId(Guid value, out ProcessRunId runId)
    {
        try
        {
            runId = new ProcessRunId(value);
            return true;
        }
        catch (ArgumentException)
        {
            runId = default;
            return false;
        }
    }

    private static bool TryCreateStepInstanceId(Guid value, out ProcessStepInstanceId stepInstanceId)
    {
        try
        {
            stepInstanceId = new ProcessStepInstanceId(value);
            return true;
        }
        catch (ArgumentException)
        {
            stepInstanceId = default;
            return false;
        }
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
