using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryAgentContextContributor(
    ICognitiveMemoryRecallOrchestrator recallOrchestrator,
    ICognitiveMemoryAutomationSettingsService settingsService) : IAgentContextContributor
{
    private static readonly Regex ProjectIdMarkerRegex = new(
        @"\b(?:CognitiveMemoryProjectId|ProjectId)\s*[:=]\s*(?<projectId>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public AgentContextContributorDescriptor Descriptor { get; } = new(
        new AgentContextContributorId("cognitive-memory.context"),
        "Cognitive Memory",
        Order: 50);

    public async ValueTask<AgentContextContributionResult> ContributeAsync(
        AgentContextContributionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = await settingsService.GetAsync(cancellationToken);
        var accessDecision = CognitiveMemoryModelAccessPolicy.Evaluate(settings, request.Provider);
        if (!accessDecision.IsAllowed)
        {
            return AgentContextContributionResult.Skipped(BuildModelAccessTraceMetadata(
                request.Provider,
                settings,
                accessDecision));
        }

        if (!TryResolveProjectId(request, out var projectId))
        {
            return AgentContextContributionResult.Skipped(new Dictionary<string, string>
            {
                ["reason"] = "project-scope-not-provided",
                ["modelAccessMode"] = settings.ModelAccessMode.ToString(),
                ["providerProfileId"] = request.Provider.Id.ToString("D")
            });
        }

        var query = BuildRecallQuery(request);
        if (string.IsNullOrWhiteSpace(query))
        {
            return AgentContextContributionResult.Skipped(new Dictionary<string, string>
            {
                ["reason"] = "empty-query",
                ["modelAccessMode"] = settings.ModelAccessMode.ToString(),
                ["providerProfileId"] = request.Provider.Id.ToString("D")
            });
        }

        try
        {
            var result = await recallOrchestrator.RecallAsync(
                new CognitiveMemoryRecallRequest(
                    projectId,
                    query,
                    CognitiveMemoryRecallIntentKind.Implementation,
                    CognitiveMemoryRecallMode.FocusedTaskContext,
                    new CognitiveMemoryPolicyContext(
                        projectId,
                        request.Agent.Id.ToString("D"),
                        CognitiveMemoryAccessLevel.Project,
                        new CognitiveMemoryPolicyProfileId("maf-context"),
                        CognitiveMemoryRiskLevel.Low,
                        request.Policy.SuppressApprovalRequirements),
                    new CognitiveMemoryRecallBudget(
                        coarseCandidateLimit: 24,
                        graphExpansionDepth: 1,
                        vectorResultLimit: 8,
                        focusLimit: 8,
                        detailItemLimit: 8,
                        contextCharacterBudget: 8000,
                        maxSourceBytes: 20000)),
                cancellationToken);
            var contextText = RenderContextPack(result.ContextPack);
            if (string.IsNullOrWhiteSpace(contextText))
            {
                return AgentContextContributionResult.Skipped(new Dictionary<string, string>
                {
                    ["reason"] = "empty-context-pack",
                    ["traceId"] = result.TraceId.ToString("D")
                });
            }

            return AgentContextContributionResult.Provided(
                [new AgentContextMessage(AgentContextMessageRole.System, contextText)],
                new Dictionary<string, string>
                {
                    ["traceId"] = result.TraceId.ToString("D"),
                    ["contextPackId"] = result.ContextPack.Id.Value.ToString("D"),
                    ["includedSections"] = result.ContextPack.Sections.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["queryLength"] = query.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["modelAccessMode"] = settings.ModelAccessMode.ToString(),
                    ["providerProfileId"] = request.Provider.Id.ToString("D"),
                    ["providerKind"] = request.Provider.Kind.ToString(),
                    ["providerIsLocal"] = accessDecision.IsLocalProvider.ToString()
                });
        }
        catch (InvalidOperationException exception)
        {
            return AgentContextContributionResult.Skipped(new Dictionary<string, string>
            {
                ["reason"] = "cognitive-memory-unavailable",
                ["message"] = exception.Message
            });
        }
    }

    private static IReadOnlyDictionary<string, string> BuildModelAccessTraceMetadata(
        ProviderProfile provider,
        CognitiveMemoryAutomationSettings settings,
        CognitiveMemoryModelAccessDecision decision)
        => new Dictionary<string, string>
        {
            ["reason"] = decision.Reason,
            ["modelAccessMode"] = settings.ModelAccessMode.ToString(),
            ["providerProfileId"] = provider.Id.ToString("D"),
            ["providerKind"] = provider.Kind.ToString(),
            ["providerIsLocal"] = decision.IsLocalProvider.ToString()
        };

    private static bool TryResolveProjectId(
        AgentContextContributionRequest request,
        out Guid projectId)
    {
        if (request.Policy.WorkspaceScope.Kind == WorkspaceScopeKind.Project &&
            Guid.TryParse(request.Policy.WorkspaceScope.Key, out projectId))
        {
            return true;
        }

        foreach (var message in request.RequestMessages.Where(message => message.Role == AgentContextMessageRole.User))
        {
            var match = ProjectIdMarkerRegex.Match(message.Text);
            if (match.Success &&
                Guid.TryParse(match.Groups["projectId"].Value, out projectId))
            {
                return true;
            }
        }

        projectId = Guid.Empty;
        return false;
    }

    private static string BuildRecallQuery(AgentContextContributionRequest request)
    {
        var userMessages = request.RequestMessages
            .Where(message => message.Role == AgentContextMessageRole.User)
            .Select(message => NormalizeRecallQuery(message.Text))
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList();
        return string.Join(Environment.NewLine, userMessages).Trim();
    }

    private static string NormalizeRecallQuery(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var questionLines = ExtractQuestionLines(lines);
        if (questionLines.Count > 0)
        {
            return string.Join(Environment.NewLine, questionLines).Trim();
        }

        var filteredLines = lines
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !ProjectIdMarkerRegex.IsMatch(line))
            .Where(line => !IsPromptControlLine(line))
            .ToList();
        return string.Join(Environment.NewLine, filteredLines).Trim();
    }

    private static List<string> ExtractQuestionLines(IReadOnlyList<string> lines)
    {
        var result = new List<string>();
        var capture = false;
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("Question:", StringComparison.OrdinalIgnoreCase))
            {
                capture = true;
                var question = line["Question:".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(question))
                {
                    result.Add(question);
                }

                continue;
            }

            if (!capture || string.IsNullOrWhiteSpace(line) || IsPromptControlLine(line))
            {
                continue;
            }

            result.Add(line);
        }

        return result;
    }

    private static bool IsPromptControlLine(string line)
        => line.StartsWith("Answer using ", StringComparison.OrdinalIgnoreCase)
           || line.StartsWith("If no memory context ", StringComparison.OrdinalIgnoreCase)
           || line.StartsWith("Return concise JSON", StringComparison.OrdinalIgnoreCase)
           || line.StartsWith("Project key:", StringComparison.OrdinalIgnoreCase)
           || line.StartsWith("Use only ", StringComparison.OrdinalIgnoreCase);

    private static string RenderContextPack(CognitiveMemoryRecallContextPack contextPack)
    {
        var sections = contextPack.Sections
            .Where(section => !string.IsNullOrWhiteSpace(section.Content))
            .Take(8)
            .Select(RenderContextSection);
        var body = string.Join(Environment.NewLine + Environment.NewLine, sections);
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        return $"Cognitive Memory context pack: {contextPack.Title}\n{contextPack.Summary}\n\n{body}".Trim();
    }

    private static string RenderContextSection(CognitiveMemoryRecallContextSection section)
    {
        var locators = section.SourceRefs
            .Where(sourceRef => sourceRef.IncludedInContext)
            .Select(sourceRef => sourceRef.Locator)
            .Where(locator => !string.IsNullOrWhiteSpace(locator))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
        var sourceLine = locators.Count == 0
            ? string.Empty
            : $"{Environment.NewLine}Source locators: {string.Join("; ", locators)}{Environment.NewLine}";
        return $"## {section.Title}{sourceLine}{section.Content.Trim()}";
    }
}

public sealed class CognitiveMemoryRecallWorkflowExecutor(
    ICognitiveMemoryRecallOrchestrator recallOrchestrator) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor { get; } = CognitiveMemoryWorkflowExecutorDescriptors.Recall;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = DeserializeSettings<CognitiveMemoryRecallWorkflowExecutorSettings>(context.SettingsJson);
        var query = string.IsNullOrWhiteSpace(settings.Query)
            ? WorkflowInputText(input)
            : settings.Query;
        if (settings.ProjectId == Guid.Empty)
        {
            throw new InvalidOperationException("Cognitive Memory recall workflow executor requires ProjectId.");
        }

        var result = await recallOrchestrator.RecallAsync(
            new CognitiveMemoryRecallRequest(
                settings.ProjectId,
                CognitiveMemoryGuard.EnsureText(query, nameof(settings.Query)),
                settings.Intent,
                settings.Mode,
                new CognitiveMemoryPolicyContext(
                    settings.ProjectId,
                    "workflow-executor",
                    CognitiveMemoryAccessLevel.Project,
                    new CognitiveMemoryPolicyProfileId("workflow"),
                    CognitiveMemoryRiskLevel.Low,
                    AllowRestrictedContent: false),
                new CognitiveMemoryRecallBudget(
                    coarseCandidateLimit: 24,
                    graphExpansionDepth: 1,
                    vectorResultLimit: 8,
                    focusLimit: 6,
                    detailItemLimit: 6,
                    contextCharacterBudget: Math.Clamp(settings.ContextCharacterBudget, 1000, 16000),
                    maxSourceBytes: 32000)),
            cancellationToken);
        return Result(context, new
        {
            traceId = result.TraceId,
            contextPackId = result.ContextPack.Id.Value,
            result.ContextPack.Title,
            result.ContextPack.Summary,
            warnings = result.Warnings,
            sections = result.ContextPack.Sections.Select(section => new
            {
                section.SectionKind,
                section.Title,
                section.Content
            })
        });
    }

    internal static WorkflowNodeExecutionResult Result(WorkflowExecutorExecutionContext context, object payload)
        => new(context.Node.Id, JsonSerializer.Serialize(payload, CognitiveMemoryAdvancedJson.Options), CognitiveMemoryWorkflowExecutorDescriptors.JsonShape);

    internal static T DeserializeSettings<T>(string json)
        where T : new()
        => string.IsNullOrWhiteSpace(json)
            ? new T()
            : JsonSerializer.Deserialize<T>(json, CognitiveMemoryAdvancedJson.Options) ?? new T();

    internal static string WorkflowInputText(WorkflowNodeInput input)
    {
        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(input.PayloadJson);
            if (document.RootElement.ValueKind == JsonValueKind.String)
            {
                return document.RootElement.GetString() ?? string.Empty;
            }

            if (document.RootElement.TryGetProperty("query", out var query))
            {
                return query.ValueKind == JsonValueKind.String ? query.GetString() ?? string.Empty : query.GetRawText();
            }

            if (document.RootElement.TryGetProperty("text", out var text))
            {
                return text.ValueKind == JsonValueKind.String ? text.GetString() ?? string.Empty : text.GetRawText();
            }
        }
        catch (JsonException)
        {
            return input.PayloadJson;
        }

        return input.PayloadJson;
    }
}

public sealed class CognitiveMemoryProbeWorkflowExecutor(
    ICognitiveMemoryProbeService probeService) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor { get; } = CognitiveMemoryWorkflowExecutorDescriptors.Probe;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = CognitiveMemoryRecallWorkflowExecutor.DeserializeSettings<CognitiveMemoryProbeWorkflowExecutorSettings>(context.SettingsJson);
        if (settings.ProjectId == Guid.Empty)
        {
            throw new InvalidOperationException("Cognitive Memory probe workflow executor requires ProjectId.");
        }

        var question = string.IsNullOrWhiteSpace(settings.Question)
            ? CognitiveMemoryRecallWorkflowExecutor.WorkflowInputText(input)
            : settings.Question;
        var policyContext = new CognitiveMemoryPolicyContext(
            settings.ProjectId,
            "workflow-executor",
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("workflow"),
            CognitiveMemoryRiskLevel.Low,
            AllowRestrictedContent: false);
        var session = await probeService.StartAsync(
            new CognitiveMemoryProbeStartRequest(settings.ProjectId, settings.SessionTitle, policyContext),
            cancellationToken);
        var result = await probeService.AskAsync(
            new CognitiveMemoryProbeAskRequest(
                session.Id,
                question,
                CognitiveMemoryRecallIntentKind.Testing,
                new CognitiveMemoryRecallBudget(24, 1, 8, 6, 6, 5000, 32000)),
            cancellationToken);
        return CognitiveMemoryRecallWorkflowExecutor.Result(context, new
        {
            sessionId = session.Id,
            turnId = result.Turn.Id,
            recallTraceId = result.Turn.RecallTraceId,
            result.Turn.AnswerSummary,
            result.Turn.WarningCount
        });
    }
}

public sealed class CognitiveMemoryLearningProposalWorkflowExecutor(
    ICognitiveMemoryEpistemicDriveService epistemicDriveService) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor { get; } = CognitiveMemoryWorkflowExecutorDescriptors.LearningProposal;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        _ = input;
        var settings = CognitiveMemoryRecallWorkflowExecutor.DeserializeSettings<CognitiveMemoryLearningWorkflowExecutorSettings>(context.SettingsJson);
        if (settings.ProjectId == Guid.Empty)
        {
            throw new InvalidOperationException("Cognitive Memory learning proposal workflow executor requires ProjectId.");
        }

        var proposals = await epistemicDriveService.ScanAsync(
            new CognitiveMemoryEpistemicScanRequest(
                settings.ProjectId,
                new CognitiveMemoryPolicyContext(
                    settings.ProjectId,
                    "workflow-executor",
                    CognitiveMemoryAccessLevel.Project,
                    new CognitiveMemoryPolicyProfileId("workflow"),
                    CognitiveMemoryRiskLevel.Low,
                    AllowRestrictedContent: false),
                "workflow-executor"),
            cancellationToken);
        return CognitiveMemoryRecallWorkflowExecutor.Result(context, new
        {
            proposalCount = proposals.Count,
            proposals = proposals.Select(item => new
            {
                item.Id,
                item.Title,
                item.Status,
                item.NeedBucket,
                item.DisplayPriorityProjection
            })
        });
    }
}

public static class CognitiveMemoryWorkflowExecutorDescriptors
{
    public static WorkflowValueShape JsonShape { get; } = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "JSON payload");

    public static WorkflowExecutorDescriptor Recall { get; } = Create(
        CognitiveMemoryWorkflowExecutorIds.Recall,
        "Cognitive Memory recall",
        "Retrieves a bounded, traceable Cognitive Memory context pack for workflow use.",
        "psychology",
        new CognitiveMemoryRecallWorkflowExecutorSettings());

    public static WorkflowExecutorDescriptor Probe { get; } = Create(
        CognitiveMemoryWorkflowExecutorIds.Probe,
        "Cognitive Memory probe",
        "Asks a durable probe question and stores the recall trace.",
        "quiz",
        new CognitiveMemoryProbeWorkflowExecutorSettings());

    public static WorkflowExecutorDescriptor LearningProposal { get; } = Create(
        CognitiveMemoryWorkflowExecutorIds.LearningProposal,
        "Cognitive Memory learning proposal",
        "Scans memory evidence and creates approval-gated learning proposals.",
        "school",
        new CognitiveMemoryLearningWorkflowExecutorSettings());

    private static WorkflowExecutorDescriptor Create<TSettings>(
        WorkflowExecutorId id,
        string name,
        string description,
        string icon,
        TSettings defaultSettings)
        => new(
            id,
            name,
            description,
            WorkflowExecutorCategoryKind.Data,
            icon,
            $"builtin.{id.Value}",
            WorkflowValueShape.Text,
            JsonShape,
            "{\"type\":\"object\"}",
            JsonSerializer.Serialize(defaultSettings, CognitiveMemoryAdvancedJson.Options),
            WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 90, CaptureOutputArtifact = true },
            IsImplemented: true)
        {
            Source = WorkflowExecutorSourceDescriptor.BuiltIn(),
            Availability = WorkflowExecutorAvailabilityDescriptor.Available()
        };
}
