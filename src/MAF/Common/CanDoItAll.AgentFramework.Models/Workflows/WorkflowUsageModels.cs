using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

[JsonConverter(typeof(WorkflowUsageObservationIdJsonConverter))]
public readonly record struct WorkflowUsageObservationId
{
    public WorkflowUsageObservationId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Workflow usage observation id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static WorkflowUsageObservationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public enum WorkflowUsageStatus
{
    Observed,
    Estimated,
    Unavailable,
    MissingAfterProviderActivity
}

public enum WorkflowPricingStatus
{
    Known,
    Unknown
}

public enum WorkflowUsagePricingProvenance
{
    ProviderReported,
    PricingProfileSnapshot,
    LegacyAggregate,
    Unavailable
}

public enum WorkflowUsageProducerKind
{
    LlmComponent,
    Executor,
    PluginExecutor,
    LegacyAggregate
}

public sealed record WorkflowUsageObservation(
    WorkflowUsageObservationId Id,
    WorkflowRunId? RunId,
    WorkflowId WorkflowId,
    WorkflowVersionId VersionId,
    WorkflowNodeId NodeId,
    WorkflowExecutorId? ExecutorId,
    WorkflowComponentId? ComponentId,
    WorkflowUsageProducerKind ProducerKind,
    Guid InvocationId,
    int Attempt,
    Guid? ProviderProfileId,
    string ProviderName,
    ProviderKind? ProviderKind,
    ProviderTransportKind? TransportKind,
    string Model,
    string SourcePhase,
    WorkflowUsageStatus UsageStatus,
    WorkflowPricingStatus PricingStatus,
    WorkflowUsagePricingProvenance PricingProvenance,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    int ToolCallCount,
    decimal? CostUsd,
    string PricingProfileHash,
    string PricingVersion,
    string ProviderRequestId,
    string ProviderResponseId,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset RecordedAtUtc,
    WorkflowLaunchOrigin? Origin);

public sealed record WorkflowUsageObservationContext(
    WorkflowRunId? RunId,
    WorkflowId WorkflowId,
    WorkflowVersionId VersionId,
    WorkflowNodeId NodeId,
    WorkflowExecutorId? ExecutorId,
    WorkflowComponentId? ComponentId,
    WorkflowUsageProducerKind ProducerKind,
    Guid InvocationId,
    int Attempt,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc)
{
    public WorkflowLaunchOrigin? Origin { get; init; }
}

public static class WorkflowUsageObservationFactory
{
    public const string LegacyMetricsSourcePhase = "legacy-workflow-usage-metrics";
    public const string ProviderResponseMetricsSourcePhase = "provider-response-metrics";
    public const string PricingSnapshotVersion = "workflow-pricing-v1";

    public static IReadOnlyList<WorkflowUsageObservation> FromProviderObservations(
        WorkflowUsageObservationContext context,
        ProviderProfile provider,
        string fallbackModel,
        IEnumerable<ProviderUsageObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(observations);

        return observations
            .Select(observation => FromProviderObservation(context, provider, fallbackModel, observation))
            .ToArray();
    }

    public static WorkflowUsageObservation FromProviderObservation(
        WorkflowUsageObservationContext context,
        ProviderProfile provider,
        string fallbackModel,
        ProviderUsageObservation observation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(observation);
        ValidateUsageDimensions(
            observation.InputTokens,
            observation.CachedInputTokens,
            observation.OutputTokens,
            observation.ReasoningTokens,
            observation.TotalTokens,
            observation.ToolCallCount);
        if (observation.ProviderCostUsd is < 0m || observation.CalculatedCostUsd is < 0m)
        {
            throw new InvalidOperationException(
                $"Provider usage observation '{observation.Id:D}' contains a negative cost.");
        }

        var providerName = string.IsNullOrWhiteSpace(observation.ProviderName)
            ? provider.Name
            : observation.ProviderName.Trim();
        var model = string.IsNullOrWhiteSpace(observation.Model)
            ? fallbackModel.Trim()
            : observation.Model.Trim();
        var pricing = ResolvePricing(observation, provider, providerName, model);
        var fact = new WorkflowUsageObservation(
            new WorkflowUsageObservationId(observation.Id),
            context.RunId,
            context.WorkflowId,
            context.VersionId,
            context.NodeId,
            context.ExecutorId,
            context.ComponentId,
            context.ProducerKind,
            context.InvocationId,
            context.Attempt,
            provider.Id,
            providerName,
            observation.ProviderKind,
            observation.TransportKind,
            model,
            NormalizeRequired(observation.SourcePhase, ProviderResponseMetricsSourcePhase),
            MapUsageStatus(observation.UsageStatus),
            pricing.Status,
            pricing.Provenance,
            observation.InputTokens,
            observation.CachedInputTokens,
            observation.OutputTokens,
            observation.ReasoningTokens,
            observation.TotalTokens,
            observation.ToolCallCount,
            pricing.CostUsd,
            pricing.ProfileHash,
            pricing.Version,
            observation.ProviderRequestId?.Trim() ?? string.Empty,
            observation.ProviderResponseId?.Trim() ?? string.Empty,
            context.StartedAtUtc,
            context.CompletedAtUtc,
            observation.CreatedAtUtc,
            context.Origin);

        WorkflowUsageObservationValidator.ThrowIfInvalid(fact);
        return fact;
    }

    public static WorkflowUsageObservation FromProviderResponseMetrics(
        WorkflowUsageObservationContext context,
        ProviderProfile provider,
        string model,
        int inputTokens,
        int cachedInputTokens,
        int outputTokens,
        int reasoningTokens,
        int totalTokens,
        int toolCallCount,
        DateTimeOffset recordedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(provider);
        ValidateUsageDimensions(
            inputTokens,
            cachedInputTokens,
            outputTokens,
            reasoningTokens,
            totalTokens,
            toolCallCount);
        var hasUsage = inputTokens > 0 || cachedInputTokens > 0 || outputTokens > 0 ||
                       reasoningTokens > 0 || totalTokens > 0 || toolCallCount > 0;
        var observation = new ProviderUsageObservation(
            CreateDeterministicObservationId(context, ProviderResponseMetricsSourcePhase, ordinal: 0).Value,
            recordedAtUtc,
            provider.Name,
            provider.Kind,
            model,
            provider.Transport,
            ProviderResponseMetricsSourcePhase,
            hasUsage
                ? ProviderUsageObservationStatus.EstimatedFromMetric
                : ProviderUsageObservationStatus.MissingAfterProviderActivity,
            inputTokens,
            cachedInputTokens,
            outputTokens,
            reasoningTokens,
            totalTokens,
            toolCallCount);

        return FromProviderObservation(context, provider, model, observation);
    }

    public static IReadOnlyList<WorkflowUsageObservation> FromLegacyMetrics(
        WorkflowUsageObservationContext context,
        WorkflowUsageMetrics metrics,
        DateTimeOffset recordedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(metrics);
        var totalTokens = checked(metrics.InputTokens + metrics.OutputTokens);
        ValidateUsageDimensions(
            metrics.InputTokens,
            metrics.CachedInputTokens,
            metrics.OutputTokens,
            reasoningTokens: 0,
            totalTokens,
            toolCallCount: 0);
        if (metrics.KnownObservationCount < 0 ||
            metrics.UnknownObservationCount < 0 ||
            metrics.CostUsd < 0m)
        {
            throw new InvalidOperationException("Legacy workflow usage metrics contain invalid counts or cost.");
        }

        var observations = new List<WorkflowUsageObservation>(2);
        if (metrics.KnownObservationCount > 0 ||
            metrics.InputTokens > 0 ||
            metrics.CachedInputTokens > 0 ||
            metrics.OutputTokens > 0)
        {
            observations.Add(CreateLegacyObservation(
                context,
                metrics,
                WorkflowUsageStatus.Estimated,
                WorkflowPricingStatus.Known,
                metrics.CostUsd,
                recordedAtUtc,
                ordinal: 0));
        }

        if (metrics.UnknownObservationCount > 0)
        {
            observations.Add(CreateLegacyObservation(
                context,
                metrics with
                {
                    InputTokens = 0,
                    CachedInputTokens = 0,
                    OutputTokens = 0,
                    CostUsd = 0m
                },
                WorkflowUsageStatus.Unavailable,
                WorkflowPricingStatus.Unknown,
                CostUsd: null,
                recordedAtUtc,
                ordinal: 1));
        }

        return observations;
    }

    private static WorkflowUsageObservation CreateLegacyObservation(
        WorkflowUsageObservationContext context,
        WorkflowUsageMetrics metrics,
        WorkflowUsageStatus usageStatus,
        WorkflowPricingStatus pricingStatus,
        decimal? CostUsd,
        DateTimeOffset recordedAtUtc,
        int ordinal)
    {
        var fact = new WorkflowUsageObservation(
            CreateDeterministicObservationId(context, LegacyMetricsSourcePhase, ordinal),
            context.RunId,
            context.WorkflowId,
            context.VersionId,
            context.NodeId,
            context.ExecutorId,
            context.ComponentId,
            WorkflowUsageProducerKind.LegacyAggregate,
            context.InvocationId,
            context.Attempt,
            ProviderProfileId: null,
            NormalizeRequired(metrics.ProviderName, "legacy-provider"),
            ProviderKind: null,
            TransportKind: null,
            NormalizeRequired(metrics.Model, "legacy-model"),
            LegacyMetricsSourcePhase,
            usageStatus,
            pricingStatus,
            pricingStatus == WorkflowPricingStatus.Known
                ? WorkflowUsagePricingProvenance.LegacyAggregate
                : WorkflowUsagePricingProvenance.Unavailable,
            metrics.InputTokens,
            metrics.CachedInputTokens,
            metrics.OutputTokens,
            ReasoningTokens: 0,
            TotalTokens: checked(metrics.InputTokens + metrics.OutputTokens),
            ToolCallCount: 0,
            CostUsd,
            PricingProfileHash: string.Empty,
            PricingVersion: string.Empty,
            ProviderRequestId: string.Empty,
            ProviderResponseId: string.Empty,
            context.StartedAtUtc,
            context.CompletedAtUtc,
            recordedAtUtc,
            context.Origin);

        WorkflowUsageObservationValidator.ThrowIfInvalid(fact);
        return fact;
    }

    private static WorkflowUsagePricingResolution ResolvePricing(
        ProviderUsageObservation observation,
        ProviderProfile provider,
        string providerName,
        string model)
    {
        var profileHash = string.IsNullOrWhiteSpace(observation.PricingProfileHash)
            ? CreatePricingProfileHash(provider)
            : observation.PricingProfileHash.Trim();
        var pricingVersion = string.IsNullOrWhiteSpace(observation.PricingVersion)
            ? PricingSnapshotVersion
            : observation.PricingVersion.Trim();
        if (observation.ProviderCostUsd is >= 0m)
        {
            return new(
                WorkflowPricingStatus.Known,
                observation.ProviderCostUsd.Value,
                WorkflowUsagePricingProvenance.ProviderReported,
                profileHash,
                pricingVersion);
        }

        if (observation.CalculatedCostUsd is >= 0m)
        {
            return new(
                WorkflowPricingStatus.Known,
                observation.CalculatedCostUsd.Value,
                WorkflowUsagePricingProvenance.PricingProfileSnapshot,
                profileHash,
                pricingVersion);
        }

        if (MapUsageStatus(observation.UsageStatus) is WorkflowUsageStatus.Observed or WorkflowUsageStatus.Estimated &&
            ProviderPricingCalculator.TryCalculate(
                providerName,
                model,
                observation.InputTokens,
                observation.CachedInputTokens,
                observation.CacheWriteTokens,
                ProviderPricingCalculator.ResolveBillableOutputTokens(
                    observation.InputTokens,
                    observation.OutputTokens,
                    observation.TotalTokens),
                provider.ModelPrices,
                out var cost))
        {
            return new(
                WorkflowPricingStatus.Known,
                cost.TotalUsd,
                WorkflowUsagePricingProvenance.PricingProfileSnapshot,
                profileHash,
                pricingVersion);
        }

        return new(
            WorkflowPricingStatus.Unknown,
            CostUsd: null,
            WorkflowUsagePricingProvenance.Unavailable,
            profileHash,
            pricingVersion);
    }

    private static string CreatePricingProfileHash(ProviderProfile provider)
    {
        var prices = string.Join(
            ';',
            provider.ModelPrices
                .OrderBy(price => price.Model, StringComparer.OrdinalIgnoreCase)
                .Select(price => string.Join(
                    ':',
                    price.Model.Trim().ToUpperInvariant(),
                    price.InputPerMillionTokensUsd.ToString(CultureInfo.InvariantCulture),
                    price.CachedInputPerMillionTokensUsd.ToString(CultureInfo.InvariantCulture),
                    price.OutputPerMillionTokensUsd.ToString(CultureInfo.InvariantCulture),
                    FormatNullable(price.CacheWritePerMillionTokensUsd),
                    FormatNullable(price.LongContextThresholdTokens),
                    FormatNullable(price.LongContextInputPerMillionTokensUsd),
                    FormatNullable(price.LongContextCachedInputPerMillionTokensUsd),
                    FormatNullable(price.LongContextCacheWritePerMillionTokensUsd),
                    FormatNullable(price.LongContextOutputPerMillionTokensUsd))));
        var canonical = string.Join(
            '|',
            provider.Id.ToString("D"),
            provider.Kind,
            provider.Transport,
            provider.IsPrivateProvider,
            prices);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string FormatNullable<T>(T? value) where T : struct, IFormattable
    {
        return value?.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static WorkflowUsageStatus MapUsageStatus(ProviderUsageObservationStatus status)
        => status switch
        {
            ProviderUsageObservationStatus.Observed or ProviderUsageObservationStatus.ObservedFromMetric =>
                WorkflowUsageStatus.Observed,
            ProviderUsageObservationStatus.EstimatedFromMetric => WorkflowUsageStatus.Estimated,
            ProviderUsageObservationStatus.MissingAfterProviderActivity =>
                WorkflowUsageStatus.MissingAfterProviderActivity,
            ProviderUsageObservationStatus.UsageUnavailable => WorkflowUsageStatus.Unavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Provider usage status is not defined.")
        };

    private static WorkflowUsageObservationId CreateDeterministicObservationId(
        WorkflowUsageObservationContext context,
        string sourcePhase,
        int ordinal)
    {
        var canonical = string.Join(
            '|',
            context.RunId?.ToString() ?? string.Empty,
            context.WorkflowId,
            context.VersionId,
            context.NodeId,
            context.InvocationId.ToString("D"),
            context.Attempt.ToString(CultureInfo.InvariantCulture),
            sourcePhase,
            ordinal.ToString(CultureInfo.InvariantCulture));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return new WorkflowUsageObservationId(new Guid(hash.AsSpan(0, 16)));
    }

    private static void ValidateUsageDimensions(
        int inputTokens,
        int cachedInputTokens,
        int outputTokens,
        int reasoningTokens,
        int totalTokens,
        int toolCallCount)
    {
        if (inputTokens < 0 ||
            cachedInputTokens < 0 ||
            cachedInputTokens > inputTokens ||
            outputTokens < 0 ||
            reasoningTokens < 0 ||
            totalTokens < 0 ||
            totalTokens < (long)inputTokens + outputTokens ||
            toolCallCount < 0)
        {
            throw new InvalidOperationException(
                "Workflow usage dimensions cannot be negative, cached input cannot exceed input, and total tokens cannot be less than input plus output tokens.");
        }
    }

    private static string NormalizeRequired(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

internal sealed record WorkflowUsagePricingResolution(
    WorkflowPricingStatus Status,
    decimal? CostUsd,
    WorkflowUsagePricingProvenance Provenance,
    string ProfileHash,
    string Version);

public static class WorkflowUsageCompatibilityProjection
{
    public static WorkflowUsageMetrics? Project(
        IReadOnlyList<WorkflowUsageObservation> observations,
        string fallbackProviderName,
        string fallbackModel)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (observations.Count == 0)
        {
            return null;
        }

        var providerNames = observations
            .Select(observation => observation.ProviderName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var models = observations
            .Select(observation => observation.Model)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var fullyKnownCount = observations.Count(observation =>
            IsUsageKnown(observation) && observation.PricingStatus == WorkflowPricingStatus.Known);

        return new WorkflowUsageMetrics(
            providerNames.Length == 1 ? providerNames[0] : fallbackProviderName,
            models.Length == 1 ? models[0] : fallbackModel,
            observations.Sum(observation => observation.InputTokens),
            observations.Sum(observation => observation.CachedInputTokens),
            observations.Sum(observation => observation.OutputTokens),
            observations
                .Where(observation => observation.PricingStatus == WorkflowPricingStatus.Known)
                .Sum(observation => observation.CostUsd ?? 0m))
        {
            KnownObservationCount = fullyKnownCount,
            UnknownObservationCount = observations.Count - fullyKnownCount
        };
    }

    public static bool IsUsageKnown(WorkflowUsageObservation observation)
        => observation.UsageStatus is WorkflowUsageStatus.Observed or WorkflowUsageStatus.Estimated;
}

public static class WorkflowUsageObservationValidator
{
    public static void ThrowIfInvalid(WorkflowUsageObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!Enum.IsDefined(observation.ProducerKind) ||
            !Enum.IsDefined(observation.UsageStatus) ||
            !Enum.IsDefined(observation.PricingStatus) ||
            !Enum.IsDefined(observation.PricingProvenance))
        {
            throw new InvalidOperationException($"Workflow usage observation '{observation.Id}' contains an undefined status value.");
        }

        if (observation.InvocationId == Guid.Empty || observation.Attempt <= 0)
        {
            throw new InvalidOperationException($"Workflow usage observation '{observation.Id}' requires an invocation id and positive attempt.");
        }

        if (string.IsNullOrWhiteSpace(observation.ProviderName) ||
            string.IsNullOrWhiteSpace(observation.Model) ||
            string.IsNullOrWhiteSpace(observation.SourcePhase))
        {
            throw new InvalidOperationException($"Workflow usage observation '{observation.Id}' requires provider, model, and source phase.");
        }

        if (observation.InputTokens < 0 ||
            observation.CachedInputTokens < 0 ||
            observation.CachedInputTokens > observation.InputTokens ||
            observation.OutputTokens < 0 ||
            observation.ReasoningTokens < 0 ||
            observation.TotalTokens < 0 ||
            observation.TotalTokens < (long)observation.InputTokens + observation.OutputTokens ||
            observation.ToolCallCount < 0)
        {
            throw new InvalidOperationException($"Workflow usage observation '{observation.Id}' contains invalid usage dimensions.");
        }

        if (observation.PricingStatus == WorkflowPricingStatus.Known && observation.CostUsd is not >= 0m)
        {
            throw new InvalidOperationException($"Workflow usage observation '{observation.Id}' has known pricing without a non-negative cost.");
        }

        if (observation.PricingStatus == WorkflowPricingStatus.Unknown && observation.CostUsd is not null)
        {
            throw new InvalidOperationException($"Workflow usage observation '{observation.Id}' has unknown pricing with a cost value.");
        }

        if (observation.StartedAtUtc is { } startedAtUtc &&
            observation.CompletedAtUtc is { } completedAtUtc &&
            completedAtUtc < startedAtUtc)
        {
            throw new InvalidOperationException($"Workflow usage observation '{observation.Id}' completes before it starts.");
        }
    }

    public static void ThrowIfNotPersistable(WorkflowUsageObservation observation)
    {
        ThrowIfInvalid(observation);
        if (observation.RunId is null)
        {
            throw new WorkflowUsageObservationCorrelationException(observation.Id);
        }
    }
}

public static class WorkflowUsageObservationCorrelator
{
    public static WorkflowUsageObservation Correlate(
        WorkflowUsageObservation observation,
        WorkflowRunId runId,
        WorkflowId workflowId,
        WorkflowVersionId versionId,
        WorkflowLaunchOrigin? origin,
        WorkflowNodeId? expectedNodeId = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var correlated = observation with
        {
            RunId = observation.RunId ?? runId,
            Origin = observation.Origin ?? origin
        };
        if (correlated.RunId != runId ||
            correlated.WorkflowId != workflowId ||
            correlated.VersionId != versionId ||
            expectedNodeId is { } nodeId && correlated.NodeId != nodeId)
        {
            throw new InvalidOperationException(
                $"Workflow usage observation '{correlated.Id}' does not match run '{runId}'" +
                (expectedNodeId is null ? "." : $" node '{expectedNodeId}'."));
        }

        WorkflowUsageObservationValidator.ThrowIfInvalid(correlated);
        return correlated;
    }
}

public sealed class WorkflowUsageObservationException : InvalidOperationException
{
    public WorkflowUsageObservationException(
        string message,
        Exception innerException,
        IReadOnlyList<WorkflowUsageObservation> observations)
        : base(message, innerException)
    {
        Observations = observations ?? throw new ArgumentNullException(nameof(observations));
    }

    public IReadOnlyList<WorkflowUsageObservation> Observations { get; }
}

public sealed class WorkflowUsageObservationConflictException : InvalidOperationException
{
    public WorkflowUsageObservationConflictException(WorkflowUsageObservationId observationId)
        : base($"Workflow usage observation '{observationId}' has the same id as a different immutable fact.")
    {
        ObservationId = observationId;
    }

    public WorkflowUsageObservationId ObservationId { get; }
}

public sealed class WorkflowUsageObservationCorrelationException : InvalidOperationException
{
    public WorkflowUsageObservationCorrelationException(WorkflowUsageObservationId observationId)
        : base($"Workflow usage observation '{observationId}' must be correlated to a workflow run before persistence.")
    {
        ObservationId = observationId;
    }

    public WorkflowUsageObservationId ObservationId { get; }
}
