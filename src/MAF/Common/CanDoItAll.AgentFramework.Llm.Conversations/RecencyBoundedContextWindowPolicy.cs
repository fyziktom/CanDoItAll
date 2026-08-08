using CanDoItAll.AgentFramework.Llm.Abstractions;

namespace CanDoItAll.AgentFramework.Llm.Conversations;

/// <summary>
/// Default non-destructive context-window selection: system entries are always kept (in transcript
/// order), the newest entry is always kept, and older non-system entries are added newest-first while
/// both the message-count and character bounds allow. The canonical transcript is never modified —
/// selection shapes only the outbound invocation window. Summarizing policies plug into the same seam
/// and stay non-destructive by construction.
/// </summary>
public sealed class RecencyBoundedContextWindowPolicy : ILlmConversationContextWindowPolicy
{
    public IReadOnlyList<LlmMessage> SelectWindow(LlmConversationContextWindowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Entries.IsDefaultOrEmpty)
        {
            return [];
        }

        var newestEntry = request.Entries[^1];
        var systemEntries = request.Entries
            .Where(entry => entry.Role == LlmMessageRole.System && entry.EntryId != newestEntry.EntryId)
            .ToList();

        // The newest entry (the pending user message) is non-negotiable; system entries yield slots and
        // budget to it but are otherwise always preferred over older conversational entries.
        var remainingSlots = request.MaximumMessages - 1;
        while (systemEntries.Count > remainingSlots)
        {
            systemEntries.RemoveAt(systemEntries.Count - 1);
        }

        var remainingCharacters = (long)request.MaximumTotalCharacters
                                  - newestEntry.Text.Length
                                  - systemEntries.Sum(entry => (long)entry.Text.Length);
        remainingSlots -= systemEntries.Count;

        var selectedRecent = new List<LlmConversationTranscriptEntry>();
        for (var index = request.Entries.Length - 2; index >= 0 && remainingSlots > 0; index--)
        {
            var entry = request.Entries[index];
            if (entry.Role == LlmMessageRole.System)
            {
                continue;
            }

            if (entry.Text.Length > remainingCharacters)
            {
                break;
            }

            selectedRecent.Add(entry);
            remainingCharacters -= entry.Text.Length;
            remainingSlots--;
        }

        selectedRecent.Reverse();
        var window = new List<LlmMessage>(systemEntries.Count + selectedRecent.Count + 1);
        window.AddRange(systemEntries.Select(ToMessage));
        window.AddRange(selectedRecent.Select(ToMessage));
        window.Add(ToMessage(newestEntry));
        return window;

        static LlmMessage ToMessage(LlmConversationTranscriptEntry entry) => new(entry.Role, entry.Text);
    }
}
