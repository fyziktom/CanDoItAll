using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private const string InputAttachmentAnalysisModelParameterConfigurationJson = """{"modelParameters":{"numPredict":384,"think":false}}""";

    private async Task<PreparedInputAttachments> PrepareInputAttachmentsAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        string prompt,
        AgentRuntimeExecutionOptions runtimeOptions,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken)
    {
        var attachments = runtimeOptions.InputAttachments ?? [];
        if (attachments.Count == 0)
        {
            return new PreparedInputAttachments(prompt, runtimeOptions);
        }

        var openAiCredentialOverride = ResolveOpenAiCredentialOverride(provider);
        var effectiveProvider = ManagedSeedProviderFallbacks.Apply(agent, provider, openAiCredentialOverride);
        var selectedModel = ManagedSeedProviderFallbacks.ResolveModel(agent, effectiveProvider, openAiCredentialOverride);
        if (string.IsNullOrWhiteSpace(selectedModel))
        {
            return new PreparedInputAttachments(prompt, runtimeOptions);
        }

        if (ProviderFeatureService.ResolveFeatureMatrixForModel(effectiveProvider, selectedModel).SupportsVision)
        {
            return new PreparedInputAttachments(prompt, runtimeOptions);
        }

        var imageModel = ResolveProviderImageAnalysisModel(effectiveProvider, selectedModel);
        if (!ProviderFeatureService.ResolveFeatureMatrixForModel(effectiveProvider, imageModel).SupportsVision)
        {
            return new PreparedInputAttachments(prompt, runtimeOptions);
        }

        await progressCallback(
            ExecutionState.Preparing,
            "Input attachments",
            $"Analyzing {attachments.Count:N0} request-scoped image attachment(s) with provider image-analysis model '{imageModel}' before running text model '{selectedModel}'.");

        var analyses = new List<InputAttachmentAnalysis>(attachments.Count);
        foreach (var attachment in attachments)
        {
            var analysisPrompt = BuildInputAttachmentAnalysisPrompt(prompt, attachment);
            var result = await providerRuntimeGateway.RunProviderImageChatAsync(
                    effectiveProvider,
                    new ProviderTestChatRequest(
                        imageModel,
                        string.Empty,
                        [],
                        analysisPrompt),
                    imageModel,
                    [new ProviderChatAttachment(
                        attachment.Name,
                        attachment.ContentType,
                        attachment.Bytes)],
                    InputAttachmentAnalysisModelParameterConfigurationJson,
                    cancellationToken)
                .ConfigureAwait(false);

            analyses.Add(new InputAttachmentAnalysis(
                attachment.Name,
                attachment.SourcePath,
                result.Model,
                result.ResponseText,
                result.InputTokens,
                result.OutputTokens));
        }

        await progressCallback(
            ExecutionState.Preparing,
            "Input attachments",
            $"Prepared visual evidence for {analyses.Count:N0} request-scoped image attachment(s); continuing with text model '{selectedModel}'.");

        return new PreparedInputAttachments(
            AppendInputAttachmentAnalysis(prompt, analyses),
            runtimeOptions with { InputAttachments = [] },
            analyses.Select(analysis => CreateInputAttachmentUsageObservation(effectiveProvider, analysis)).ToList());
    }

    private static string BuildInputAttachmentAnalysisPrompt(
        string userPrompt,
        AgentRuntimeInputAttachment attachment)
    {
        return $"""
            Analyze one image attachment for a software delivery agent. Use only visible evidence.
            Report concise facts about UI state, visible text, object positions, colors, and any observable software behavior.
            Do not speculate beyond the image.

            Attachment name: {attachment.Name}
            Attachment path: {attachment.SourcePath}
            User request: {userPrompt}
            """;
    }

    private static string AppendInputAttachmentAnalysis(
        string prompt,
        IReadOnlyList<InputAttachmentAnalysis> analyses)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Request-scoped image attachment evidence:");
        builder.AppendLine("Use this provider-generated visual evidence as authoritative for the attached image content.");
        builder.AppendLine("Do not infer image contents from file names or artifact paths, and do not call image-analysis tools again for these same attachments unless the user explicitly asks for another pass.");
        foreach (var analysis in analyses)
        {
            builder.AppendLine($"- Attachment: {analysis.Name}");
            builder.AppendLine($"  Path: {analysis.SourcePath}");
            builder.AppendLine($"  Vision model: {analysis.Model}");
            builder.AppendLine($"  Provider usage: inputTokens={analysis.InputTokens}, outputTokens={analysis.OutputTokens}");
            builder.AppendLine("  Visible evidence:");
            builder.AppendLine(IndentAnalysis(analysis.Analysis));
        }

        builder.AppendLine();
        builder.AppendLine("User request:");
        builder.AppendLine(prompt.Trim());
        return builder.ToString();
    }

    private static ProviderUsageObservation CreateInputAttachmentUsageObservation(
        ProviderProfile provider,
        InputAttachmentAnalysis analysis)
    {
        var inputTokens = Math.Max(0, analysis.InputTokens);
        var outputTokens = Math.Max(0, analysis.OutputTokens);
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ProviderName: provider.Name,
            ProviderKind: provider.Kind,
            Model: analysis.Model,
            TransportKind: provider.Transport,
            SourcePhase: ProviderUsageSourcePhases.InputAttachmentAnalysis,
            UsageStatus: inputTokens + outputTokens > 0
                ? ProviderUsageObservationStatus.Observed
                : ProviderUsageObservationStatus.UsageUnavailable,
            InputTokens: inputTokens,
            CachedInputTokens: 0,
            OutputTokens: outputTokens,
            ReasoningTokens: 0,
            TotalTokens: inputTokens + outputTokens,
            ToolCallCount: 0)
        {
            DiagnosticsJson = JsonSerializer.Serialize(
                new Dictionary<string, string>
                {
                    ["source"] = "request-scoped image attachment analysis",
                    ["attachment"] = analysis.SourcePath
                },
                SerializerOptions)
        };
    }

    private static AgentRuntimeResponse AttachPreparedInputUsageObservations(
        AgentRuntimeResponse response,
        IReadOnlyList<ProviderUsageObservation>? usageObservations)
    {
        if (usageObservations is null || usageObservations.Count == 0)
        {
            return response;
        }

        return response with
        {
            UsageObservations = usageObservations
                .Concat(response.UsageObservations)
                .ToList()
        };
    }

    private static string IndentAnalysis(string analysis)
    {
        var normalized = string.IsNullOrWhiteSpace(analysis)
            ? "No visible evidence was returned by the provider image-analysis model."
            : analysis.Trim();
        return string.Join(
            Environment.NewLine,
            normalized
                .Split(["\r\n", "\n"], StringSplitOptions.None)
                .Select(line => $"    {line}"));
    }

    private sealed record PreparedInputAttachments(
        string Prompt,
        AgentRuntimeExecutionOptions RuntimeOptions,
        IReadOnlyList<ProviderUsageObservation>? UsageObservations = null);

    private sealed record InputAttachmentAnalysis(
        string Name,
        string SourcePath,
        string Model,
        string Analysis,
        int InputTokens,
        int OutputTokens);
}
