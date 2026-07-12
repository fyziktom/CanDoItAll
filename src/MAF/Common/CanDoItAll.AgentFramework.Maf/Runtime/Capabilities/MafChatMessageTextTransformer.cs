using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafChatMessageTextTransformer
{
    public static IReadOnlyList<ChatMessage> Apply(
        AgentContextRequestMessageTransformation? transformation,
        IReadOnlyList<ChatMessage> originalMessages,
        AgentContextContributorId contributorId)
    {
        if (transformation is null)
        {
            return originalMessages;
        }

        var transformed = originalMessages.ToArray();
        foreach (var replacement in transformation.TextReplacements)
        {
            if (replacement.MessageIndex < 0 || replacement.MessageIndex >= transformed.Length)
            {
                throw new AgentContextContributionException(
                    contributorId,
                    $"Context contributor returned an invalid request message index '{replacement.MessageIndex}'.");
            }

            transformed[replacement.MessageIndex] = ReplaceText(
                transformed[replacement.MessageIndex],
                replacement.Text);
        }

        return transformed;
    }

    private static ChatMessage ReplaceText(ChatMessage original, string replacementText)
    {
        var replaced = false;
        var contents = new List<AIContent>(original.Contents.Count);
        foreach (var content in original.Contents)
        {
            if (content is TextContent)
            {
                if (!replaced)
                {
                    contents.Add(new TextContent(replacementText));
                    replaced = true;
                }

                continue;
            }

            contents.Add(content);
        }

        if (!replaced)
        {
            contents.Insert(0, new TextContent(replacementText));
        }

        return new ChatMessage(original.Role, contents)
        {
            AdditionalProperties = original.AdditionalProperties,
            AuthorName = original.AuthorName,
            CreatedAt = original.CreatedAt,
            MessageId = original.MessageId,
            RawRepresentation = null
        };
    }
}
