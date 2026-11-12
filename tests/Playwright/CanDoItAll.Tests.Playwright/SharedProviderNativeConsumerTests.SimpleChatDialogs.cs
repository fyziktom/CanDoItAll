using System.Text.Json;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class SharedProviderNativeConsumerTests {
    private static async Task RenameAndArchiveSimpleChatAsync(SharedProviderConsumerFixture fixture, JsonElement original, string marker, string suffix) {
        var id = original.GetProperty("id").GetGuid();
        var title = "Retained renamed " + marker;
        await fixture.NavigateAsync($"/agents?tab=simple-chats&simpleChatView=conversations&conversationId={id:D}");
        await fixture.Page.GetByTestId($"llm-chat-rename-{id:D}").ClickAsync();
        var rename = fixture.Page.GetByTestId("llm-chat-rename-dialog");
        await rename.GetByTestId("llm-chat-rename-title").FillAsync(title);
        await rename.GetByTestId("llm-chat-rename-confirm").ClickAsync();
        await rename.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        var renamed = await fixture.GetAsync($"api/llm-conversations/{id:D}");
        Assert.Equal(title, renamed.GetProperty("title").GetString());
        Assert.Equal(original.GetProperty("concurrencyToken").GetInt64() + 1, renamed.GetProperty("concurrencyToken").GetInt64());
        Assert.Equal(original.GetProperty("transcriptRevision").GetInt64() + 1, renamed.GetProperty("transcriptRevision").GetInt64());
        await fixture.NavigateAsync($"/agents?tab=simple-chats&simpleChatView=conversations&conversationId={id:D}");
        await fixture.Page.GetByTestId($"llm-chat-archive-{id:D}").ClickAsync();
        var archive = fixture.Page.GetByTestId("llm-chat-archive-dialog");
        var confirm = archive.GetByRole(AriaRole.Button, new() { Name = "Archive", Exact = true });
        await Assertions.Expect(confirm).ToBeDisabledAsync();
        await archive.Locator("input").FillAsync(title);
        await confirm.ClickAsync();
        await archive.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        var archived = await fixture.GetAsync($"api/llm-conversations/{id:D}");
        Assert.Equal("archived", archived.GetProperty("status").GetString());
        Assert.Equal(renamed.GetProperty("concurrencyToken").GetInt64() + 1, archived.GetProperty("concurrencyToken").GetInt64());
        Assert.Equal(renamed.GetProperty("transcriptRevision").GetInt64(), archived.GetProperty("transcriptRevision").GetInt64());
        Assert.Equal(original.GetProperty("definitionId").GetGuid(), archived.GetProperty("definitionId").GetGuid());
        Assert.Equal(original.GetProperty("definitionRevision").GetInt32(), archived.GetProperty("definitionRevision").GetInt32());
        Assert.Equal(original.GetProperty("messages").GetRawText(), archived.GetProperty("messages").GetRawText());
        await fixture.NavigateAsync($"/agents?tab=simple-chats&simpleChatView=conversations&conversationId={id:D}");
        await Assertions.Expect(fixture.Page.GetByTestId("llm-chat-conversation-workspace")).ToContainTextAsync(title);
        await Assertions.Expect(fixture.Page.GetByTestId("llm-chat-send")).ToBeDisabledAsync();
        await fixture.ScreenshotAsync("consumer-simple-chat-archived-" + suffix);
        await fixture.EvidenceAsync("consumer-simple-chat-dialogs-" + suffix, new {
            ConversationId = id, Title = title, Origin = archived.GetProperty("origin"), Status = archived.GetProperty("status"),
            BeforeConcurrency = original.GetProperty("concurrencyToken"), RenamedConcurrency = renamed.GetProperty("concurrencyToken"),
            ArchivedConcurrency = archived.GetProperty("concurrencyToken"), DefinitionRevision = archived.GetProperty("definitionRevision"),
            MessagesSha256 = SharedProviderConsumerFixture.Hash(archived.GetProperty("messages").GetRawText())
        });
    }

}
