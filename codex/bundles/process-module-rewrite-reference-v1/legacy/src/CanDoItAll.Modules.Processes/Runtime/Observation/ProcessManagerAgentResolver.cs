using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessManagerAgentResolver
{
    public static Guid? ResolveConfiguredTechnicalAgentId(
        Guid? managerAgentId,
        string? managerAgentName,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents)
        => ResolveConfiguredManager(managerAgentId, managerAgentName, managerOptions, agents).ResolvedTechnicalAgentId;

    public static Guid? ResolveAssignedTechnicalAgentId(
        IReadOnlyList<ProcessRunAssignmentViewModel> assignments,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents)
        => ResolveAssignedManager(assignments, managerOptions, agents).ResolvedTechnicalAgentId;

    public static Guid? ResolveFallbackTechnicalAgentId(
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents)
        => ResolveFallbackManager(managerOptions, agents).ResolvedTechnicalAgentId;

    public static ProcessManagerAgentResolution ResolveConfiguredManager(
        Guid? managerAgentId,
        string? managerAgentName,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents)
    {
        if (managerAgentId.HasValue)
        {
            var optionCandidates = managerOptions
                .Where(option =>
                    option.TechnicalAgentId.HasValue &&
                    (option.PartyId == managerAgentId.Value ||
                     option.TechnicalAgentId == managerAgentId.Value))
                .Select(option => new ManagerCandidate(
                    option.TechnicalAgentId!.Value,
                    option.DisplayName,
                    100,
                    "configured manager option",
                    IsCapabilityBacked: false))
                .ToList();
            if (optionCandidates.Count > 0)
            {
                return ResolveUniqueCandidate(
                    optionCandidates,
                    candidate => ProcessManagerAgentResolutionReasonCode.ConfiguredManagerOption,
                    ProcessManagerAgentResolutionReasonCode.AmbiguousConfiguredManager,
                    ProcessManagerAgentResolutionReasonCode.NoConfiguredManagerCandidate,
                    "Configured manager id matched multiple technical manager bindings.",
                    "Configured manager id resolved to the bound technical agent.");
            }

            var agentCandidates = agents
                .Where(agent => agent.Id == managerAgentId.Value)
                .Select(agent => new ManagerCandidate(
                    agent.Id,
                    agent.Name,
                    100,
                    "configured agent id",
                    IsCapabilityBacked: HasManagerCapability(agent)))
                .ToList();
            if (agentCandidates.Count > 0)
            {
                return ResolveUniqueCandidate(
                    agentCandidates,
                    candidate => ProcessManagerAgentResolutionReasonCode.ConfiguredAgentId,
                    ProcessManagerAgentResolutionReasonCode.AmbiguousConfiguredManager,
                    ProcessManagerAgentResolutionReasonCode.NoConfiguredManagerCandidate,
                    "Configured manager id matched multiple Agent Framework agents.",
                    "Configured manager id resolved directly to an Agent Framework agent.");
            }
        }

        if (!string.IsNullOrWhiteSpace(managerAgentName))
        {
            var normalizedName = managerAgentName.Trim();
            var optionCandidates = managerOptions
                .Where(option =>
                    option.TechnicalAgentId.HasValue &&
                    string.Equals(option.DisplayName, normalizedName, StringComparison.OrdinalIgnoreCase))
                .Select(option => new ManagerCandidate(
                    option.TechnicalAgentId!.Value,
                    option.DisplayName,
                    95,
                    "configured manager name",
                    IsCapabilityBacked: false))
                .ToList();
            if (optionCandidates.Count > 0)
            {
                return ResolveUniqueCandidate(
                    optionCandidates,
                    candidate => ProcessManagerAgentResolutionReasonCode.ConfiguredManagerName,
                    ProcessManagerAgentResolutionReasonCode.AmbiguousConfiguredManager,
                    ProcessManagerAgentResolutionReasonCode.NoConfiguredManagerCandidate,
                    "Configured manager name matched multiple technical manager bindings.",
                    "Configured manager name resolved to the bound technical agent.");
            }

            var agentCandidates = agents
                .Where(agent => string.Equals(agent.Name, normalizedName, StringComparison.OrdinalIgnoreCase))
                .Select(agent => new ManagerCandidate(
                    agent.Id,
                    agent.Name,
                    90,
                    "configured agent name",
                    HasManagerCapability(agent)))
                .ToList();
            if (agentCandidates.Count > 0)
            {
                return ResolveUniqueCandidate(
                    agentCandidates,
                    candidate => ProcessManagerAgentResolutionReasonCode.ConfiguredAgentName,
                    ProcessManagerAgentResolutionReasonCode.AmbiguousConfiguredManager,
                    ProcessManagerAgentResolutionReasonCode.NoConfiguredManagerCandidate,
                    "Configured manager name matched multiple Agent Framework agents.",
                    "Configured manager name resolved directly to an Agent Framework agent.");
            }
        }

        return ProcessManagerAgentResolution.NotResolved(
            ProcessManagerAgentResolutionReasonCode.NoConfiguredManagerCandidate,
            "No configured manager id or manager name resolved to a technical agent.");
    }

    public static ProcessManagerAgentResolution ResolveAssignedManager(
        IReadOnlyList<ProcessRunAssignmentViewModel> assignments,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents)
    {
        var candidates = assignments
            .Where(item => !item.IsCapabilityGap)
            .Select(item => new
            {
                Assignment = item,
                TechnicalAgentId = ResolveManagerOptionByIdentifier(item.PartyId, managerOptions)?.TechnicalAgentId ??
                                   ResolveAgentByIdentifier(item.PartyId, agents)?.Id,
                Score = ResolveManagerAssignmentScore(item)
            })
            .Where(item => item.TechnicalAgentId.HasValue && item.Score > 0)
            .GroupBy(item => item.TechnicalAgentId!.Value)
            .Select(group =>
            {
                var best = group
                    .OrderByDescending(item => item.Score)
                    .ThenBy(item => item.Assignment.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .First();
                return new ManagerCandidate(
                    best.TechnicalAgentId!.Value,
                    best.Assignment.DisplayName,
                    best.Score,
                    $"selected-run assignment role '{best.Assignment.RoleDisplayName}'",
                    IsCapabilityBacked: false);
            })
            .ToList();

        return ResolveUniqueCandidate(
            candidates,
            candidate => ProcessManagerAgentResolutionReasonCode.SelectedRunAssignment,
            ProcessManagerAgentResolutionReasonCode.AmbiguousSelectedRunAssignment,
            ProcessManagerAgentResolutionReasonCode.NoSelectedRunAssignmentCandidate,
            "Selected run has multiple equally strong manager-like assignments.",
            "Selected-run assignment resolved the manager technical agent.");
    }

    public static ProcessManagerAgentResolution ResolveFallbackManager(
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents)
    {
        var optionCandidates = managerOptions
            .Where(option => option.TechnicalAgentId.HasValue)
            .Select(option => new ManagerCandidate(
                option.TechnicalAgentId!.Value,
                option.DisplayName,
                ResolveManagerOptionScore(option),
                "manager option fallback",
                IsCapabilityBacked: false))
            .Where(candidate => candidate.Score > 0)
            .ToList();
        if (optionCandidates.Count > 0)
        {
            return ResolveUniqueCandidate(
                optionCandidates,
                candidate => ProcessManagerAgentResolutionReasonCode.FallbackManagerOption,
                ProcessManagerAgentResolutionReasonCode.AmbiguousFallbackManager,
                ProcessManagerAgentResolutionReasonCode.NoFallbackManagerCandidate,
                "Fallback manager options are ambiguous; select or configure the intended manager.",
                "Fallback manager option resolved the manager technical agent.");
        }

        var agentCandidates = agents
            .Select(agent => new ManagerCandidate(
                agent.Id,
                agent.Name,
                ResolveAgentManagerScore(agent),
                "Agent Framework fallback",
                HasManagerCapability(agent)))
            .Where(candidate => candidate.Score > 0)
            .ToList();

        return ResolveUniqueCandidate(
            agentCandidates,
            candidate => candidate.IsCapabilityBacked
                ? ProcessManagerAgentResolutionReasonCode.FallbackAgentCapability
                : ProcessManagerAgentResolutionReasonCode.FallbackAgentTextSignal,
            ProcessManagerAgentResolutionReasonCode.AmbiguousFallbackManager,
            ProcessManagerAgentResolutionReasonCode.NoFallbackManagerCandidate,
            "Fallback manager agents are ambiguous; select or configure the intended manager.",
            "Fallback Agent Framework signals resolved the manager technical agent.");
    }

    private static ProcessManagerAgentResolution ResolveUniqueCandidate(
        IReadOnlyList<ManagerCandidate> candidates,
        Func<ManagerCandidate, ProcessManagerAgentResolutionReasonCode> resolvedReasonCode,
        ProcessManagerAgentResolutionReasonCode ambiguousReasonCode,
        ProcessManagerAgentResolutionReasonCode emptyReasonCode,
        string ambiguousSummary,
        string resolvedSummary)
    {
        if (candidates.Count == 0)
        {
            return ProcessManagerAgentResolution.NotResolved(
                emptyReasonCode,
                "No manager candidate matched this resolution phase.");
        }

        var orderedCandidates = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var topScore = orderedCandidates[0].Score;
        var topCandidates = orderedCandidates
            .Where(candidate => candidate.Score == topScore)
            .ToList();
        if (topCandidates.Count > 1)
        {
            return ProcessManagerAgentResolution.Ambiguous(
                ambiguousReasonCode,
                ClampConfidence(topScore),
                $"{ambiguousSummary} Candidates: {FormatCandidateList(topCandidates)}.",
                topCandidates.Select(FormatCandidate).ToList());
        }

        var selected = topCandidates[0];
        return ProcessManagerAgentResolution.Resolved(
            selected.TechnicalAgentId,
            resolvedReasonCode(selected),
            ClampConfidence(selected.Score),
            $"{resolvedSummary} Candidate: {FormatCandidate(selected)}.",
            [FormatCandidate(selected)]);
    }

    private static ProcessManagerAgentOption? ResolveManagerOptionByIdentifier(
        Guid? managerId,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions)
    {
        if (!managerId.HasValue)
        {
            return null;
        }

        return managerOptions.FirstOrDefault(option =>
            option.PartyId == managerId.Value ||
            option.TechnicalAgentId == managerId.Value);
    }

    private static AgentDefinition? ResolveAgentByIdentifier(
        Guid? managerId,
        IReadOnlyList<AgentDefinition> agents)
    {
        return managerId.HasValue
            ? agents.FirstOrDefault(agent => agent.Id == managerId.Value)
            : null;
    }

    private static int ResolveManagerAssignmentScore(ProcessRunAssignmentViewModel assignment)
    {
        var score = 0;
        score = Math.Max(score, ResolveManagerTextScore(assignment.RoleDisplayName));
        score = Math.Max(score, ResolveManagerTextScore(assignment.DisplayName));
        score = Math.Max(score, ResolveManagerTextScore(assignment.BindingReason));
        return assignment.AllowsDirectMessaging
            ? score + 5
            : score;
    }

    private static int ResolveManagerOptionScore(ProcessManagerAgentOption option)
    {
        var score = 0;
        score = Math.Max(score, ResolveManagerTextScore(option.DisplayName));
        score = Math.Max(score, ResolveManagerTextScore(option.BindingSummary));
        return score;
    }

    private static int ResolveAgentManagerScore(AgentDefinition agent)
    {
        var score = 0;
        score = Math.Max(score, ResolveManagerCapabilityScore(agent));
        score = Math.Max(score, agent.Tags.Any(ContainsManagerToken) ? 80 : 0);
        score = Math.Max(score, ResolveManagerTextScore(agent.Name));
        score = Math.Max(score, ResolveManagerTextScore(agent.RoleTitle));
        return score;
    }

    private static int ResolveManagerCapabilityScore(AgentDefinition agent)
    {
        var score = 0;
        foreach (var capability in agent.Capabilities)
        {
            var tokens = Tokenize(capability.CapabilityKey);
            if (tokens.Contains("manager", StringComparer.OrdinalIgnoreCase) &&
                (tokens.Contains("process", StringComparer.OrdinalIgnoreCase) ||
                 tokens.Contains("chat", StringComparer.OrdinalIgnoreCase) ||
                 tokens.Contains("directive", StringComparer.OrdinalIgnoreCase)))
            {
                score = Math.Max(score, 120);
                continue;
            }

            if (tokens.Contains("orchestrator", StringComparer.OrdinalIgnoreCase) &&
                tokens.Contains("process", StringComparer.OrdinalIgnoreCase))
            {
                score = Math.Max(score, 100);
            }
        }

        return score;
    }

    private static bool HasManagerCapability(AgentDefinition agent)
        => ResolveManagerCapabilityScore(agent) > 0;

    private static int ResolveManagerTextScore(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (value.Contains("process manager", StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (value.Contains("delivery manager", StringComparison.OrdinalIgnoreCase))
        {
            return 90;
        }

        if (value.Contains("manager", StringComparison.OrdinalIgnoreCase))
        {
            return 70;
        }

        if (value.Contains("orchestrator", StringComparison.OrdinalIgnoreCase))
        {
            return 60;
        }

        return value.Contains("lead", StringComparison.OrdinalIgnoreCase)
            ? 50
            : 0;
    }

    private static bool ContainsManagerToken(string value)
        => Tokenize(value).Any(token =>
            string.Equals(token, "manager", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "lead", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "orchestrator", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> Tokenize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(
            [' ', '-', '_', '/', '\\', '.', ':', ';', ',', '(', ')', '[', ']'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static int ClampConfidence(int score)
        => Math.Clamp(score, 0, 100);

    private static string FormatCandidateList(IEnumerable<ManagerCandidate> candidates)
        => string.Join(", ", candidates.Select(candidate => candidate.Label));

    private static string FormatCandidate(ManagerCandidate candidate)
        => $"{candidate.Label} [{candidate.Source}; score {candidate.Score}]";

    private sealed record ManagerCandidate(
        Guid TechnicalAgentId,
        string Label,
        int Score,
        string Source,
        bool IsCapabilityBacked);
}

internal sealed record ProcessManagerAgentResolution(
    Guid? TechnicalAgentId,
    ProcessManagerAgentResolutionReasonCode ReasonCode,
    int Confidence,
    string Summary,
    IReadOnlyList<string> CandidateSummaries)
{
    public Guid? ResolvedTechnicalAgentId => IsResolved ? TechnicalAgentId : null;

    public bool IsResolved => TechnicalAgentId.HasValue && !IsAmbiguous;

    public bool IsAmbiguous => ReasonCode is
        ProcessManagerAgentResolutionReasonCode.AmbiguousConfiguredManager or
        ProcessManagerAgentResolutionReasonCode.AmbiguousSelectedRunAssignment or
        ProcessManagerAgentResolutionReasonCode.AmbiguousFallbackManager;

    public static ProcessManagerAgentResolution Resolved(
        Guid technicalAgentId,
        ProcessManagerAgentResolutionReasonCode reasonCode,
        int confidence,
        string summary,
        IReadOnlyList<string> candidateSummaries)
        => new(technicalAgentId, reasonCode, confidence, summary, candidateSummaries);

    public static ProcessManagerAgentResolution Ambiguous(
        ProcessManagerAgentResolutionReasonCode reasonCode,
        int confidence,
        string summary,
        IReadOnlyList<string> candidateSummaries)
        => new(null, reasonCode, confidence, summary, candidateSummaries);

    public static ProcessManagerAgentResolution NotResolved(
        ProcessManagerAgentResolutionReasonCode reasonCode,
        string summary)
        => new(null, reasonCode, 0, summary, []);

    public static ProcessManagerAgentResolution NotEvaluated(string summary)
        => new(null, ProcessManagerAgentResolutionReasonCode.NotEvaluated, 0, summary, []);
}

internal enum ProcessManagerAgentResolutionReasonCode
{
    NotEvaluated = 0,
    ConfiguredManagerOption = 1,
    ConfiguredAgentId = 2,
    ConfiguredManagerName = 3,
    ConfiguredAgentName = 4,
    SelectedRunAssignment = 5,
    FallbackManagerOption = 6,
    FallbackAgentCapability = 7,
    FallbackAgentTextSignal = 8,
    NoConfiguredManagerCandidate = 20,
    NoSelectedRunAssignmentCandidate = 21,
    NoFallbackManagerCandidate = 22,
    AmbiguousConfiguredManager = 40,
    AmbiguousSelectedRunAssignment = 41,
    AmbiguousFallbackManager = 42
}
