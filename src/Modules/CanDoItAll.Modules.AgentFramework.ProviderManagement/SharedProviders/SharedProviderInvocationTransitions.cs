using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public static class SharedProviderInvocationTransitions
{
    private const int MaximumRequestIdLength = 128;
    private const int MaximumSubjectLength = 256;
    private const int MaximumTraceIdLength = 128;
    private const int MaximumCorrelationIdLength = 128;

    public static SharedProviderInvocationRecord Create(
        string requestId,
        SharedProviderPublicationId publicationId,
        Guid providerProfileId,
        string authenticatedSubject,
        AccessContextReference? accessContextReference,
        string traceId,
        string correlationId,
        SharedProviderRelayOperation operation,
        SharedProviderRoutingModelId publicModelId,
        string upstreamModelId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset deleteAfterUtc,
        AccessContextReferenceType? accessContextReferenceType = null)
    {
        SharedProviderStateGuard.PublicationId(publicationId, nameof(publicationId));
        SharedProviderStateGuard.NonEmpty(providerProfileId, nameof(providerProfileId));
        SharedProviderStateGuard.Utc(startedAtUtc, nameof(startedAtUtc));
        SharedProviderStateGuard.Utc(deleteAfterUtc, nameof(deleteAfterUtc));
        if (deleteAfterUtc <= startedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deleteAfterUtc),
                "The invocation retention timestamp must follow its start timestamp.");
        }

        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (accessContextReference.HasValue)
        {
            _ = accessContextReference.Value.Value;
        }
        if (accessContextReferenceType.HasValue)
        {
            _ = accessContextReferenceType.Value.Value;
            if (!accessContextReference.HasValue)
            {
                throw new ArgumentException(
                    "An access-context reference type requires an access-context reference.",
                    nameof(accessContextReferenceType));
            }
        }

        var expectedModelId = SharedProviderRoutingModelIdCodec.Create(publicationId, upstreamModelId);
        if (publicModelId != expectedModelId)
        {
            throw new ArgumentException(
                "The public routing model id does not identify the publication and upstream model.",
                nameof(publicModelId));
        }

        return new SharedProviderInvocationRecord
        {
            RequestId = SharedProviderStateGuard.ExactText(
                requestId,
                MaximumRequestIdLength,
                nameof(requestId)),
            PublicationId = publicationId,
            ProviderProfileId = providerProfileId,
            AuthenticatedSubject = SharedProviderStateGuard.ExactText(
                authenticatedSubject,
                MaximumSubjectLength,
                nameof(authenticatedSubject)),
            AccessContextReference = accessContextReference,
            AccessContextReferenceType = accessContextReferenceType,
            TraceId = SharedProviderStateGuard.ExactText(
                traceId,
                MaximumTraceIdLength,
                nameof(traceId)),
            CorrelationId = SharedProviderStateGuard.ExactText(
                correlationId,
                MaximumCorrelationIdLength,
                nameof(correlationId)),
            Operation = operation,
            PublicModelId = publicModelId,
            UpstreamModelId = upstreamModelId,
            StartedAtUtc = startedAtUtc,
            DeleteAfterUtc = deleteAfterUtc
        };
    }

    public static void Finalize(
        SharedProviderInvocationRecord invocation,
        SharedProviderInvocationCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(completion);
        ValidateCompletion(invocation, completion);

        if (invocation.Outcome != SharedProviderInvocationOutcome.InProgress)
        {
            if (MatchesCompletion(invocation, completion) || SharedProviderTerminalEvidence.IgnoreCancellation(invocation, completion))
            {
                return;
            }

            if (!SharedProviderTerminalEvidence.CanReconcile(invocation, completion)) {
                throw new InvalidOperationException("The invocation was already finalized with a different completion.");
            }
        }

        invocation.HistoryVersion = checked(invocation.HistoryVersion + 1);
        invocation.FinalizationRecovered = false;
        invocation.CompletedAtUtc = completion.CompletedAtUtc;
        invocation.DurationMilliseconds = checked(
            (long)(completion.CompletedAtUtc - invocation.StartedAtUtc).TotalMilliseconds);
        invocation.Outcome = completion.Outcome;
        invocation.FailureCategory = completion.FailureCategory;
        invocation.InputTokenCount = completion.InputTokenCount;
        invocation.OutputTokenCount = completion.OutputTokenCount;
        invocation.ImageCount = completion.ImageCount;
        invocation.UsageCompleteness = completion.UsageCompleteness;
        invocation.Price = completion.Price;
        invocation.PricingCompleteness = completion.PricingCompleteness;
        invocation.PriceEvidence = completion.PriceEvidence;
        invocation.CachedInputTokenCount = completion.CachedInputTokenCount;
        invocation.CacheWriteTokenCount = completion.CacheWriteTokenCount;
        invocation.ReasoningTokenCount = completion.ReasoningTokenCount;
    }

    public static bool RecoverInterruptedFinalization(
        SharedProviderInvocationRecord invocation,
        DateTimeOffset recoveredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        SharedProviderStateGuard.Utc(recoveredAtUtc, nameof(recoveredAtUtc));
        if (invocation.Outcome != SharedProviderInvocationOutcome.InProgress)
        {
            return false;
        }

        Finalize(
            invocation,
            new SharedProviderInvocationCompletion(
                SharedProviderInvocationOutcome.Failed,
                recoveredAtUtc,
                SharedProviderFailureCategory.Unavailable,
                InputTokenCount: null,
                OutputTokenCount: null,
                SharedProviderMetadataCompleteness.Unavailable,
                Price: null,
                SharedProviderMetadataCompleteness.Unavailable));
        invocation.FinalizationRecovered = true;
        return true;
    }

    private static bool MatchesCompletion(
        SharedProviderInvocationRecord invocation,
        SharedProviderInvocationCompletion completion)
        => invocation.CompletedAtUtc == completion.CompletedAtUtc &&
            invocation.Outcome == completion.Outcome &&
            invocation.FailureCategory == completion.FailureCategory &&
            invocation.InputTokenCount == completion.InputTokenCount &&
            invocation.OutputTokenCount == completion.OutputTokenCount &&
            invocation.ImageCount == completion.ImageCount &&
            invocation.UsageCompleteness == completion.UsageCompleteness &&
            invocation.Price == completion.Price &&
            invocation.PricingCompleteness == completion.PricingCompleteness &&
            invocation.PriceEvidence == completion.PriceEvidence &&
            invocation.CachedInputTokenCount == completion.CachedInputTokenCount &&
            invocation.CacheWriteTokenCount == completion.CacheWriteTokenCount &&
            invocation.ReasoningTokenCount == completion.ReasoningTokenCount;

    private static void ValidateCompletion(
        SharedProviderInvocationRecord invocation,
        SharedProviderInvocationCompletion completion)
    {
        SharedProviderStateGuard.NonEmpty(invocation.Id, nameof(invocation));
        SharedProviderStateGuard.Utc(invocation.StartedAtUtc, nameof(invocation));
        SharedProviderStateGuard.Utc(completion.CompletedAtUtc, nameof(completion));
        if (completion.CompletedAtUtc < invocation.StartedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completion),
                "The invocation completion cannot precede its start.");
        }

        var validOutcomeAndFailure = completion.Outcome switch
        {
            SharedProviderInvocationOutcome.Succeeded => completion.FailureCategory is null,
            SharedProviderInvocationOutcome.Failed => completion.FailureCategory is not null,
            SharedProviderInvocationOutcome.Cancelled =>
                completion.FailureCategory == SharedProviderFailureCategory.Cancelled,
            _ => false
        };
        if (!validOutcomeAndFailure)
        {
            throw new ArgumentException(
                "The invocation outcome and failure category are inconsistent.",
                nameof(completion));
        }

        if (completion.InputTokenCount is < 0 ||
            completion.OutputTokenCount is < 0 ||
            completion.ImageCount is <= 0 or > SharedProviderRelaySupportDescriptor.MaximumAllowedImageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(completion));
        }

        var hasInputUsage = completion.InputTokenCount.HasValue;
        var hasOutputUsage = completion.OutputTokenCount.HasValue;
        var hasImageUsage = completion.ImageCount.HasValue;
        var usageIsConsistent = invocation.Operation switch
        {
            SharedProviderRelayOperation.ChatCompletions or SharedProviderRelayOperation.Responses =>
                completion.UsageCompleteness switch
                {
                    SharedProviderMetadataCompleteness.Unavailable =>
                        !hasInputUsage && !hasOutputUsage && !hasImageUsage,
                    SharedProviderMetadataCompleteness.Partial =>
                        !hasImageUsage && hasInputUsage != hasOutputUsage,
                    SharedProviderMetadataCompleteness.Complete =>
                        !hasImageUsage && hasInputUsage && hasOutputUsage,
                    _ => false
                },
            SharedProviderRelayOperation.ImageGenerations =>
                completion.UsageCompleteness switch
                {
                    SharedProviderMetadataCompleteness.Unavailable =>
                        !hasInputUsage && !hasOutputUsage && !hasImageUsage,
                    SharedProviderMetadataCompleteness.Complete =>
                        hasImageUsage && !hasInputUsage && !hasOutputUsage,
                    _ => false
                },
            _ => false
        };
        if (!usageIsConsistent)
        {
            throw new ArgumentException(
                "The invocation usage values do not match their completeness state.",
                nameof(completion));
        }

        if (completion.CachedInputTokenCount is < 0 || completion.CacheWriteTokenCount is < 0
            || completion.ReasoningTokenCount is < 0
            || completion.CachedInputTokenCount > completion.InputTokenCount
            || completion.CacheWriteTokenCount > completion.InputTokenCount - (completion.CachedInputTokenCount ?? 0)
            || completion.ReasoningTokenCount > completion.OutputTokenCount
            || completion.PriceEvidence is { } evidence && evidence.Amount != completion.Price) {
            throw new ArgumentException("Invalid pricing or token category evidence.", nameof(completion));
        }

        if (completion.Price is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(completion));
        }

        var pricingIsConsistent = completion.PricingCompleteness switch
        {
            SharedProviderMetadataCompleteness.Unavailable => completion.Price is null,
            SharedProviderMetadataCompleteness.Partial => true,
            SharedProviderMetadataCompleteness.Complete => completion.Price is not null,
            _ => false
        };
        if (!pricingIsConsistent)
        {
            throw new ArgumentException(
                "The invocation price does not match its completeness state.",
                nameof(completion));
        }
    }
}
