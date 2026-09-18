using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.Prompts.UI.Editor;

namespace CanDoItAll.Tests.Unit.Prompts;

// The baseline comparator decides whether a newer concurrency token may be adopted, so it must never treat two
// different persisted declarations as equal and must keep treating equivalent normalized declarations as equal.
public sealed class PromptGalleryPersistedContentTests
{
    private static readonly PromptGalleryEditorSubmission Submission = new(
        "Reusable prompt",
        "Summary",
        PromptGalleryItemKind.FullPrompt,
        "design",
        "Prompt content",
        ["architecture"],
        [new PromptProviderModel("OpenAI", "gpt-5.4-mini", IsPreferred: false)],
        [PromptGalleryConsumer.Chat],
        new PromptModelRecommendations(0.2, 800, 0.9));

    [Fact]
    public void Provider_and_model_values_containing_a_delimiter_do_not_collide()
    {
        var first = Content([new PromptProviderModel("a|b", "c", IsPreferred: false)]);
        var second = Content([new PromptProviderModel("a", "b|c", IsPreferred: false)]);

        Assert.False(first.Matches(second));
        Assert.False(second.Matches(first));
        Assert.True(first.Matches(Content([new PromptProviderModel("a|b", "c", IsPreferred: false)])));
    }

    [Fact]
    public void Reordered_repeated_and_differently_cased_declarations_are_equivalent()
    {
        var first = Content(
        [
            new PromptProviderModel("OpenAI", "gpt-5.4-mini", IsPreferred: true),
            new PromptProviderModel(" anthropic ", "Claude-Sonnet-5", IsPreferred: false)
        ]);
        var second = Content(
        [
            new PromptProviderModel("ANTHROPIC", "claude-sonnet-5", IsPreferred: false),
            new PromptProviderModel("openai", " GPT-5.4-MINI ", IsPreferred: true),
            new PromptProviderModel("openai", "gpt-5.4-mini", IsPreferred: true)
        ]);

        Assert.True(first.Matches(second));
        Assert.True(second.Matches(first));
    }

    [Fact]
    public void A_changed_preference_flag_is_a_different_declaration()
    {
        var preferred = Content([new PromptProviderModel("OpenAI", "gpt-5.4-mini", IsPreferred: true)]);
        var notPreferred = Content([new PromptProviderModel("OpenAI", "gpt-5.4-mini", IsPreferred: false)]);

        Assert.False(preferred.Matches(notPreferred));
    }

    [Fact]
    public void An_added_or_removed_declaration_is_a_different_model_set()
    {
        var one = Content([new PromptProviderModel("OpenAI", "gpt-5.4-mini", IsPreferred: true)]);
        var two = Content(
        [
            new PromptProviderModel("OpenAI", "gpt-5.4-mini", IsPreferred: true),
            new PromptProviderModel("OpenAI", "gpt-5.4", IsPreferred: false)
        ]);

        Assert.False(one.Matches(two));
        Assert.False(two.Matches(one));
    }

    [Fact]
    public void Tags_and_consumers_compare_as_normalized_sets_and_scalars_compare_trimmed()
    {
        var first = PromptGalleryPersistedContent.FromSubmission(Submission with
        {
            Title = "  Reusable prompt ",
            Tags = ["Review", " architecture "],
            SupportedConsumers = [PromptGalleryConsumer.Workflow, PromptGalleryConsumer.Chat, PromptGalleryConsumer.Chat]
        });
        var second = PromptGalleryPersistedContent.FromSubmission(Submission with
        {
            Tags = ["ARCHITECTURE", "review"],
            SupportedConsumers = [PromptGalleryConsumer.Chat, PromptGalleryConsumer.Workflow]
        });

        Assert.True(first.Matches(second));
        Assert.False(first.Matches(PromptGalleryPersistedContent.FromSubmission(Submission with { Tags = ["architecture"] })));
    }

    [Fact]
    public void Details_and_submission_of_the_same_revision_match()
    {
        var itemId = Guid.NewGuid();
        var submission = Submission with
        {
            SupportedModels = [new PromptProviderModel("OpenAI", "gpt-5.4-mini", IsPreferred: true), new PromptProviderModel("Ollama", "llama", IsPreferred: false)],
            Tags = ["architecture", "review"]
        };
        var details = ScriptedPromptGalleryService.Details(itemId, content: submission);

        Assert.True(PromptGalleryPersistedContent.FromDetails(details).Matches(PromptGalleryPersistedContent.FromSubmission(submission)));
    }

    private static PromptGalleryPersistedContent Content(IReadOnlyList<PromptProviderModel> models)
        => PromptGalleryPersistedContent.FromSubmission(Submission with { SupportedModels = models });
}
