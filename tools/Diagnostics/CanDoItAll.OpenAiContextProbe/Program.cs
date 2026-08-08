using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.OpenAiContextProbe;

internal static partial class Program
{
    private const string DefaultModel = "gpt-5.4-mini";
    private const string DefaultOpenAiBaseUrl = "https://api.openai.com/v1";
    private const string OpenAiApiKeyEnvironmentVariable = "OPENAI_API_KEY";
    private const string OpenAiAdminKeyEnvironmentVariable = "OPENAI_ADMIN_KEY";
    private const int DefaultUsagePollAttempts = 4;
    private const int DefaultUsagePollDelaySeconds = 15;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<int> Main(string[] args)
    {
        var options = ProbeOptions.Parse(args);
        var apiKey = Environment.GetEnvironmentVariable(OpenAiApiKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine($"Environment variable '{OpenAiApiKeyEnvironmentVariable}' is required.");
            return 2;
        }

        var workspaceRoot = Path.GetFullPath(options.WorkspaceRoot);
        var outputPath = Path.GetFullPath(options.OutputPath);
        var startedAtUtc = DateTimeOffset.UtcNow;
        var usageClient = new OpenAiUsageApiClient(options.UsageApiKeyEnvironmentVariable, options.Model);
        var beforeUsage = await usageClient.TryQueryAsync(
            startedAtUtc.AddMinutes(-2),
            startedAtUtc.AddMinutes(1),
            CancellationToken.None);

        var runtime = new MafAgentRuntime(
            workspaceRoot,
            new ServiceCollection().BuildServiceProvider(),
            WorkspaceScopeDescriptor.Sandbox);
        var provider = CreateProvider(options.Model);
        var agent = CreateAgent(provider, options.Model);
        var variants = CreateVariants();
        var variantResults = new List<RuntimeVariantProbeResult>();

        foreach (var variant in variants)
        {
            variantResults.Add(await RunVariantAsync(
                runtime.ExecutionPort,
                agent,
                provider,
                variant,
                CancellationToken.None));
        }

        var completedAtUtc = DateTimeOffset.UtcNow;
        var expectedProviderRequests = variantResults.Count(item => item.Error is null);
        UsageApiSnapshot? afterUsage = null;
        UsageApiSnapshot? usageDelta = null;

        for (var attempt = 1; attempt <= options.UsagePollAttempts; attempt++)
        {
            afterUsage = await usageClient.TryQueryAsync(
                startedAtUtc.AddMinutes(-2),
                completedAtUtc.AddMinutes(3),
                CancellationToken.None);
            usageDelta = UsageApiSnapshot.Delta(beforeUsage, afterUsage);
            if (afterUsage.StatusCode is HttpStatusCode.OK &&
                usageDelta?.NumModelRequests >= expectedProviderRequests)
            {
                break;
            }

            if (attempt < options.UsagePollAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(options.UsagePollDelaySeconds));
            }
        }

        var report = new OpenAiContextProbeReport(
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: completedAtUtc,
            WorkspaceRoot: workspaceRoot,
            Model: options.Model,
            RuntimeVariants: variantResults,
            RuntimeTotals: RuntimeUsageTotals.From(variantResults),
            OpenAiUsageApi: new UsageApiProbeResult(
                Attempted: true,
                CredentialEnvironmentVariable: options.UsageApiKeyEnvironmentVariable,
                UsesAdminKeyEnvironmentVariable: string.Equals(
                    options.UsageApiKeyEnvironmentVariable,
                    OpenAiAdminKeyEnvironmentVariable,
                    StringComparison.Ordinal),
                Before: beforeUsage,
                After: afterUsage,
                Delta: usageDelta));

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        Console.WriteLine(outputPath);
        Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));

        return variantResults.All(item => item.Error is null) ? 0 : 1;
    }

    private static async Task<RuntimeVariantProbeResult> RunVariantAsync(
        IAgentExecutionRuntime executionPort,
        AgentDefinition agent,
        ProviderProfile provider,
        ProbeVariant variant,
        CancellationToken cancellationToken)
    {
        var progressMessages = new List<string>();
        var session = new ChatSessionRecord(
            Id: Guid.NewGuid(),
            AgentId: agent.Id,
            Title: variant.Name,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Messages: []);
        var executionOptions = new AgentRuntimeExecutionOptions(
            StructuredOutput: null,
            FinalizerMode: AgentFinalizerMode.Disabled,
            RequireStructuredOutputValidation: true,
            MaxStructuredOutputRepairAttempts: 0,
            ContextWorkspaceScope: WorkspaceScopeDescriptor.Sandbox,
            ContextIntent: variant.ContextIntent);

        try
        {
            var response = await executionPort.ExecuteAsync(
                new AgentRuntimeExecutionRequest(
                    agent,
                    provider,
                    session,
                    Capabilities: [],
                    Memory: [],
                    Prompt: "Return exactly CONTEXT_PROBE_OK. Do not call tools.",
                    RuntimeSessionKey: null,
                    ProgressCallback: (_, title, message) =>
                    {
                        progressMessages.Add($"{title}: {message}");
                        return Task.CompletedTask;
                    },
                    SuppressApprovalRequirements: true,
                    StructuredOutput: null,
                    ExecutionOptions: executionOptions),
                cancellationToken);

            return RuntimeVariantProbeResult.FromResponse(
                variant,
                response,
                progressMessages.Count);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return RuntimeVariantProbeResult.FromError(
                variant,
                WorkflowExecutorRedaction.RedactText(exception.ToString()),
                progressMessages.Count);
        }
    }

    private static AgentDefinition CreateAgent(ProviderProfile provider, string model)
    {
        var now = DateTimeOffset.UtcNow;
        var workspaceToolConfiguration = AgentWorkspaceToolAccessMetadata.Write(
            "{}",
            AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment));

        return new AgentDefinition(
            Id: Guid.NewGuid(),
            Name: "OpenAI Context Probe Agent",
            RoleTitle: "Context Probe",
            Summary: "Measures production runtime context assembly.",
            Instructions: "You are a CanDoItAll context measurement probe. Never call tools for this task. Return exactly CONTEXT_PROBE_OK.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: provider.Id,
            Model: model,
            Workload: AgentWorkloadKind.Programming,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: workspaceToolConfiguration,
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default with
            {
                CanUseTools = true,
                CanAskOtherAgents = false,
                RequiresApprovalForExternalCalls = false,
                AutoApproveExternalCallsByDefault = true
            },
            Capabilities: [],
            Tags: ["context-probe"],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static ProviderProfile CreateProvider(string model)
        => new(
            Id: Guid.NewGuid(),
            Name: "OpenAI context probe",
            Kind: ProviderKind.OpenAi,
            BaseUrl: DefaultOpenAiBaseUrl,
            ApiKeyEnvironmentVariable: OpenAiApiKeyEnvironmentVariable,
            DefaultModel: model,
            Transport: ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Probe",
            LastCheckedAtUtc: null,
            SuggestedModels: [model]);

    private static IReadOnlyList<ProbeVariant> CreateVariants()
        =>
        [
            new ProbeVariant(
                "baseline-software-development",
                AgentRuntimeContextIntent.Empty,
                "Interactive runtime with the agent's full software-development workspace tool profile."),
            new ProbeVariant(
                "process-read-context",
                CreateProcessContextIntent(ProcessOperationContractNames.ReadProcessContext),
                "Governed process step that only needs to read process context."),
            new ProbeVariant(
                "process-run-validation",
                CreateProcessContextIntent(ProcessOperationContractNames.RunValidation),
                "Governed process step that can run validation but cannot mutate product files.")
        ];

    private static AgentRuntimeContextIntent CreateProcessContextIntent(params string[] allowedOperations)
        => new(
            SourceKind: "process-step",
            SourceId: "openai-context-probe",
            ProcessRunId: Guid.NewGuid().ToString("D"),
            ProcessStepId: Guid.NewGuid().ToString("D"),
            TargetScope: ProcessOperationContractNames.ManagedOutputProduct,
            IsGovernedProcessStep: true,
            BrowserToolsAllowed: false,
            AllowsProductMutation: allowedOperations.Contains(
                ProcessOperationContractNames.MutateProductTarget,
                StringComparer.OrdinalIgnoreCase),
            WorkspaceToolProfile: null,
            WorkspaceScope: WorkspaceScopeDescriptor.Sandbox,
            AllowedOperations: allowedOperations);

    private sealed record ProbeOptions(
        string WorkspaceRoot,
        string OutputPath,
        string Model,
        string UsageApiKeyEnvironmentVariable,
        int UsagePollAttempts,
        int UsagePollDelaySeconds)
    {
        public static ProbeOptions Parse(IReadOnlyList<string> args)
        {
            var workspaceRoot = Directory.GetCurrentDirectory();
            var outputPath = Path.Combine(
                workspaceRoot,
                "codex",
                "bundles",
                "agent-context-token-budget-optimization-v1",
                "proof",
                $"real-openai-context-probe-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");
            var model = Environment.GetEnvironmentVariable("CANDOITALL_OPENAI_CONTEXT_PROBE_MODEL")
                        ?? Environment.GetEnvironmentVariable("OPENAI_MODEL")
                        ?? DefaultModel;
            var usageApiKeyEnvironmentVariable = string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(OpenAiAdminKeyEnvironmentVariable))
                ? OpenAiApiKeyEnvironmentVariable
                : OpenAiAdminKeyEnvironmentVariable;
            var usagePollAttempts = DefaultUsagePollAttempts;
            var usagePollDelaySeconds = DefaultUsagePollDelaySeconds;

            for (var index = 0; index < args.Count; index++)
            {
                switch (args[index])
                {
                    case "--workspace-root":
                        workspaceRoot = ReadRequiredArgument(args, ref index);
                        break;
                    case "--output":
                        outputPath = ReadRequiredArgument(args, ref index);
                        break;
                    case "--model":
                        model = ReadRequiredArgument(args, ref index);
                        break;
                    case "--usage-api-key-env":
                        usageApiKeyEnvironmentVariable = ReadRequiredArgument(args, ref index);
                        break;
                    case "--usage-poll-attempts":
                        usagePollAttempts = int.Parse(ReadRequiredArgument(args, ref index));
                        break;
                    case "--usage-poll-delay-seconds":
                        usagePollDelaySeconds = int.Parse(ReadRequiredArgument(args, ref index));
                        break;
                }
            }

            return new ProbeOptions(
                workspaceRoot,
                outputPath,
                model,
                usageApiKeyEnvironmentVariable,
                Math.Clamp(usagePollAttempts, 1, 20),
                Math.Clamp(usagePollDelaySeconds, 0, 120));
        }

        private static string ReadRequiredArgument(IReadOnlyList<string> args, ref int index)
        {
            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Missing value for '{args[index]}'.");
            }

            index++;
            return args[index];
        }
    }

    private sealed record ProbeVariant(
        string Name,
        AgentRuntimeContextIntent ContextIntent,
        string Description);

    private sealed record OpenAiContextProbeReport(
        DateTimeOffset StartedAtUtc,
        DateTimeOffset CompletedAtUtc,
        string WorkspaceRoot,
        string Model,
        IReadOnlyList<RuntimeVariantProbeResult> RuntimeVariants,
        RuntimeUsageTotals RuntimeTotals,
        UsageApiProbeResult OpenAiUsageApi);

    private sealed record RuntimeUsageTotals(
        int ObservedInputTokens,
        int ObservedCachedInputTokens,
        int ObservedBillableInputTokens,
        int ObservedOutputTokens,
        int ObservedTotalTokens,
        int ManifestEstimatedInputTokens,
        int ManifestToolCount,
        int ResponseCount)
    {
        public static RuntimeUsageTotals From(IReadOnlyList<RuntimeVariantProbeResult> results)
        {
            var successful = results.Where(item => item.Error is null).ToList();
            var observedInput = successful.Sum(item => item.ObservedInputTokens ?? item.ResponseInputTokens ?? 0);
            var observedCached = successful.Sum(item => item.ObservedCachedInputTokens ?? item.ResponseCachedInputTokens ?? 0);
            return new RuntimeUsageTotals(
                ObservedInputTokens: observedInput,
                ObservedCachedInputTokens: observedCached,
                ObservedBillableInputTokens: Math.Max(0, observedInput - observedCached),
                ObservedOutputTokens: successful.Sum(item => item.ObservedOutputTokens ?? item.ResponseOutputTokens ?? 0),
                ObservedTotalTokens: successful.Sum(item => item.ObservedTotalTokens ?? 0),
                ManifestEstimatedInputTokens: successful.Sum(item => item.Manifest?.Totals.EstimatedInputTokens ?? 0),
                ManifestToolCount: successful.Sum(item => item.Manifest?.Totals.ToolCount ?? 0),
                ResponseCount: successful.Count);
        }
    }

    private sealed record RuntimeVariantProbeResult(
        string Name,
        string Description,
        string? ResponsePreview,
        int? ResponseInputTokens,
        int? ResponseCachedInputTokens,
        int? ResponseOutputTokens,
        int? ToolCalls,
        int? ObservedInputTokens,
        int? ObservedCachedInputTokens,
        int? ObservedOutputTokens,
        int? ObservedReasoningTokens,
        int? ObservedTotalTokens,
        string? UsageStatus,
        ContextManifestSummary? Manifest,
        IReadOnlyList<ContextSourceSummary> ContextSources,
        int ProgressMessageCount,
        string? Error)
    {
        public static RuntimeVariantProbeResult FromResponse(
            ProbeVariant variant,
            AgentRuntimeResponse response,
            int progressMessageCount)
        {
            var usage = response.UsageObservations.FirstOrDefault();
            return new RuntimeVariantProbeResult(
                Name: variant.Name,
                Description: variant.Description,
                ResponsePreview: CreatePreview(response.ResponseText),
                ResponseInputTokens: response.InputTokens,
                ResponseCachedInputTokens: response.CachedInputTokens,
                ResponseOutputTokens: response.OutputTokens,
                ToolCalls: response.ToolCalls,
                ObservedInputTokens: usage?.InputTokens,
                ObservedCachedInputTokens: usage?.CachedInputTokens,
                ObservedOutputTokens: usage?.OutputTokens,
                ObservedReasoningTokens: usage?.ReasoningTokens,
                ObservedTotalTokens: usage?.TotalTokens,
                UsageStatus: usage?.UsageStatus.ToString(),
                Manifest: ContextManifestSummary.From(response.ContextAssemblyManifest),
                ContextSources: response.ContextAssemblyManifest?.Sources
                    .Select(ContextSourceSummary.From)
                    .ToList() ?? [],
                ProgressMessageCount: progressMessageCount,
                Error: null);
        }

        public static RuntimeVariantProbeResult FromError(
            ProbeVariant variant,
            string error,
            int progressMessageCount)
            => new(
                Name: variant.Name,
                Description: variant.Description,
                ResponsePreview: null,
                ResponseInputTokens: null,
                ResponseCachedInputTokens: null,
                ResponseOutputTokens: null,
                ToolCalls: null,
                ObservedInputTokens: null,
                ObservedCachedInputTokens: null,
                ObservedOutputTokens: null,
                ObservedReasoningTokens: null,
                ObservedTotalTokens: null,
                UsageStatus: null,
                Manifest: null,
                ContextSources: [],
                ProgressMessageCount: progressMessageCount,
                Error: CreatePreview(error, 1200));
    }

    private sealed record ContextManifestSummary(
        Guid Id,
        DateTimeOffset CreatedAtUtc,
        string ProviderName,
        ProviderKind ProviderKind,
        string Model,
        ProviderTransportKind TransportKind,
        AgentRuntimeContextManifestTotals Totals)
    {
        public static ContextManifestSummary? From(AgentRuntimeContextAssemblyManifest? manifest)
            => manifest is null
                ? null
                : new ContextManifestSummary(
                    manifest.Id,
                    manifest.CreatedAtUtc,
                    manifest.ProviderName,
                    manifest.ProviderKind,
                    manifest.Model,
                    manifest.TransportKind,
                    manifest.Totals);
    }

    private sealed record ContextSourceSummary(
        string Category,
        string SourceId,
        AgentRuntimeContextSourceDecision Decision,
        int ItemCount,
        int EstimatedTokens,
        string Reason)
    {
        public static ContextSourceSummary From(AgentRuntimeContextManifestSource source)
            => new(
                source.Category,
                source.SourceId,
                source.Decision,
                source.ItemCount,
                source.EstimatedTokens,
                source.Reason);
    }

    private sealed record UsageApiProbeResult(
        bool Attempted,
        string CredentialEnvironmentVariable,
        bool UsesAdminKeyEnvironmentVariable,
        UsageApiSnapshot? Before,
        UsageApiSnapshot? After,
        UsageApiSnapshot? Delta);

    private sealed record UsageApiSnapshot(
        HttpStatusCode? StatusCode,
        string Status,
        DateTimeOffset QueriedAtUtc,
        DateTimeOffset StartTimeUtc,
        DateTimeOffset EndTimeUtc,
        int InputTokens,
        int CachedInputTokens,
        int OutputTokens,
        int NumModelRequests,
        string? Error)
    {
        public static UsageApiSnapshot? Delta(UsageApiSnapshot? before, UsageApiSnapshot? after)
        {
            if (before?.StatusCode is not HttpStatusCode.OK ||
                after?.StatusCode is not HttpStatusCode.OK)
            {
                return null;
            }

            return new UsageApiSnapshot(
                StatusCode: HttpStatusCode.OK,
                Status: "Delta",
                QueriedAtUtc: after.QueriedAtUtc,
                StartTimeUtc: before.StartTimeUtc,
                EndTimeUtc: after.EndTimeUtc,
                InputTokens: Math.Max(0, after.InputTokens - before.InputTokens),
                CachedInputTokens: Math.Max(0, after.CachedInputTokens - before.CachedInputTokens),
                OutputTokens: Math.Max(0, after.OutputTokens - before.OutputTokens),
                NumModelRequests: Math.Max(0, after.NumModelRequests - before.NumModelRequests),
                Error: null);
        }
    }

    private sealed class OpenAiUsageApiClient(string apiKeyEnvironmentVariable, string model)
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public async Task<UsageApiSnapshot> TryQueryAsync(
            DateTimeOffset startTimeUtc,
            DateTimeOffset endTimeUtc,
            CancellationToken cancellationToken)
        {
            var queriedAtUtc = DateTimeOffset.UtcNow;
            var apiKey = Environment.GetEnvironmentVariable(apiKeyEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return CreateFailure(
                    null,
                    "Skipped",
                    queriedAtUtc,
                    startTimeUtc,
                    endTimeUtc,
                    $"Environment variable '{apiKeyEnvironmentVariable}' is not set.");
            }

            var uri = new UriBuilder($"{DefaultOpenAiBaseUrl}/organization/usage/completions")
            {
                Query = string.Join(
                    '&',
                    $"start_time={startTimeUtc.ToUnixTimeSeconds()}",
                    $"end_time={endTimeUtc.ToUnixTimeSeconds()}",
                    "bucket_width=1m",
                    "limit=20",
                    $"models={Uri.EscapeDataString(model)}")
            }.Uri;

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Trim());
            request.Headers.Accept.ParseAdd("application/json");

            try
            {
                using var response = await HttpClient.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return CreateFailure(
                        response.StatusCode,
                        response.StatusCode.ToString(),
                        queriedAtUtc,
                        startTimeUtc,
                        endTimeUtc,
                        SanitizeError(body));
                }

                var aggregate = UsageAggregate.Parse(body);
                return new UsageApiSnapshot(
                    StatusCode: response.StatusCode,
                    Status: response.StatusCode.ToString(),
                    QueriedAtUtc: queriedAtUtc,
                    StartTimeUtc: startTimeUtc,
                    EndTimeUtc: endTimeUtc,
                    InputTokens: aggregate.InputTokens,
                    CachedInputTokens: aggregate.CachedInputTokens,
                    OutputTokens: aggregate.OutputTokens,
                    NumModelRequests: aggregate.NumModelRequests,
                    Error: null);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return CreateFailure(
                    null,
                    "Exception",
                    queriedAtUtc,
                    startTimeUtc,
                    endTimeUtc,
                    SanitizeError(exception.Message));
            }
        }

        private static UsageApiSnapshot CreateFailure(
            HttpStatusCode? statusCode,
            string status,
            DateTimeOffset queriedAtUtc,
            DateTimeOffset startTimeUtc,
            DateTimeOffset endTimeUtc,
            string error)
            => new(
                StatusCode: statusCode,
                Status: status,
                QueriedAtUtc: queriedAtUtc,
                StartTimeUtc: startTimeUtc,
                EndTimeUtc: endTimeUtc,
                InputTokens: 0,
                CachedInputTokens: 0,
                OutputTokens: 0,
                NumModelRequests: 0,
                Error: CreatePreview(error, 800));
    }

    private sealed record UsageAggregate(
        int InputTokens,
        int CachedInputTokens,
        int OutputTokens,
        int NumModelRequests)
    {
        public static UsageAggregate Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            var inputTokens = 0;
            var cachedInputTokens = 0;
            var outputTokens = 0;
            var requests = 0;

            if (!document.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                return new UsageAggregate(0, 0, 0, 0);
            }

            foreach (var bucket in data.EnumerateArray())
            {
                if (!bucket.TryGetProperty("results", out var results) ||
                    results.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var result in results.EnumerateArray())
                {
                    inputTokens += ReadInt(result, "input_tokens");
                    cachedInputTokens += ReadInt(result, "input_cached_tokens");
                    outputTokens += ReadInt(result, "output_tokens");
                    requests += ReadInt(result, "num_model_requests");
                }
            }

            return new UsageAggregate(inputTokens, cachedInputTokens, outputTokens, requests);
        }

        private static int ReadInt(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return 0;
            }

            return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue)
                ? Math.Max(0, intValue)
                : 0;
        }
    }

    private static string CreatePreview(string value, int maxLength = 240)
    {
        var normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "...";
    }

    private static string SanitizeError(string value)
        => SecretPattern().Replace(value, "[REDACTED]");

    [GeneratedRegex(@"sk-[A-Za-z0-9_\-]+", RegexOptions.Compiled)]
    private static partial Regex SecretPattern();
}
