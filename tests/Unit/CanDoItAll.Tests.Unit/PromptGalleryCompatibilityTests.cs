using CanDoItAll.Modules.Prompts;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class PromptGalleryCompatibilityTests
{
    private readonly PromptGalleryCompatibilityEvaluator _evaluator = new();

    [Fact]
    public void Selection_warning_can_be_suppressed_for_the_typed_consumer()
    {
        var result = _evaluator.Evaluate(
            CreateItem(PromptGalleryItemKind.Part),
            new PromptGalleryConsumerContext(
                PromptGalleryConsumer.Chat,
                RequiredKind: PromptGalleryItemKind.FullPrompt,
                Provider: "OpenAI",
                Model: "gpt-4"),
            new HashSet<PromptCompatibilityIssueCode>
            {
                PromptCompatibilityIssueCode.ItemKindMismatch,
                PromptCompatibilityIssueCode.ProviderModelNotSupported
            });

        Assert.True(result.CanUse);
        Assert.False(result.HasVisibleWarnings);
        Assert.Collection(
            result.Issues,
            issue => AssertSuppressedWarning(issue, PromptCompatibilityIssueCode.ItemKindMismatch),
            issue => AssertSuppressedWarning(issue, PromptCompatibilityIssueCode.ProviderModelNotSupported));
    }

    [Fact]
    public void Execution_mismatches_are_errors_and_ignore_warning_preferences()
    {
        var result = _evaluator.Evaluate(
            CreateItem(PromptGalleryItemKind.Part),
            new PromptGalleryConsumerContext(
                PromptGalleryConsumer.Chat,
                PromptGalleryCompatibilityPurpose.Execution,
                PromptGalleryItemKind.FullPrompt,
                "OpenAI",
                "gpt-4"),
            new HashSet<PromptCompatibilityIssueCode>
            {
                PromptCompatibilityIssueCode.ItemKindMismatch,
                PromptCompatibilityIssueCode.ProviderModelNotSupported
            });

        Assert.False(result.CanUse);
        Assert.All(result.Issues, issue =>
        {
            Assert.Equal(PromptCompatibilitySeverity.Error, issue.Severity);
            Assert.False(issue.IsSuppressible);
            Assert.False(issue.IsSuppressed);
        });
    }

    [Fact]
    public void Archive_missing_version_and_unsupported_consumer_are_non_suppressible_errors()
    {
        var item = CreateItem(
            PromptGalleryItemKind.FullPrompt,
            archived: true,
            currentVersion: 0,
            supportedConsumers: [PromptGalleryConsumer.Chat]);

        var result = _evaluator.Evaluate(
            item,
            new PromptGalleryConsumerContext(
                PromptGalleryConsumer.Workflow,
                RequiresFinalVersion: true));

        Assert.False(result.CanUse);
        Assert.Equal(
            [
                PromptCompatibilityIssueCode.Archived,
                PromptCompatibilityIssueCode.MissingFinalVersion,
                PromptCompatibilityIssueCode.ConsumerNotSupported
            ],
            result.Issues.Select(issue => issue.Code));
        Assert.All(result.Issues, issue => Assert.False(issue.IsSuppressible));
    }

    private static PromptGalleryItemDetails CreateItem(
        PromptGalleryItemKind kind,
        bool archived = false,
        int currentVersion = 1,
        IReadOnlyList<PromptGalleryConsumer>? supportedConsumers = null)
        => new(
            Guid.NewGuid(),
            ProjectId: null,
            CollectionId: null,
            "Item",
            "Summary",
            kind,
            "",
            PromptArtifactStatus.Final,
            archived,
            "Content",
            currentVersion,
            Tags: [],
            TemplateTokens: [],
            SupportedModels: [new PromptProviderModel("OpenAI", "gpt-5")],
            SupportedConsumers: supportedConsumers ?? [],
            WarningSuppressions: [],
            Recommendations: new PromptModelRecommendations(),
            Source: new PromptGallerySourceInfo(PromptArtifactProvenance.User, null, null, null, null, null, null),
            Versions: [],
            CreatedAtUtc: DateTimeOffset.UnixEpoch,
            UpdatedAtUtc: DateTimeOffset.UnixEpoch);

    private static void AssertSuppressedWarning(
        PromptCompatibilityIssue issue,
        PromptCompatibilityIssueCode expectedCode)
    {
        Assert.Equal(expectedCode, issue.Code);
        Assert.Equal(PromptCompatibilitySeverity.Warning, issue.Severity);
        Assert.True(issue.IsSuppressible);
        Assert.True(issue.IsSuppressed);
    }
}
