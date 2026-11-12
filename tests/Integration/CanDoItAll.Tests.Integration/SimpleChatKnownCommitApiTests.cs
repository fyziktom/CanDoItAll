using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.LlmChats;

public sealed class SimpleChatKnownCommitApiTests {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [Fact]
    public async Task Turn_HTTP_preserves_the_committed_admission_operation_and_reconciliation_warning() {
        var service = DispatchProxy.Create<ILlmChatOperationApplicationService, OperationOwner>();
        var owner = (OperationOwner)(object)service;
        await using var host = await ApiTestHost.CreateAsync(false, services => services.AddSingleton(service), useInMemoryDatabase: true);
        var operationId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        using var response = await host.Client.PostAsJsonAsync($"/api/llm-conversations/{conversationId:D}/turns",
            new { operationId, expectedTranscriptRevision = 2, message = "Admitted message" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("committed-reconciliation-pending", Assert.Single(response.Headers.GetValues(LlmChatApiResults.OutcomeHeader)));
        Assert.Equal($"/api/llm-chat-operations/{operationId:D}", response.Headers.Location!.OriginalString);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(operationId, body.RootElement.GetProperty("operationId").GetGuid());
        Assert.Equal(conversationId, body.RootElement.GetProperty("conversationId").GetGuid());
        Assert.DoesNotContain("PRIVATE_ADMISSION", body.RootElement.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(1, owner.Writes);
    }

    public class OperationOwner : DispatchProxy {
        public int Writes { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            if (method?.Name != nameof(ILlmChatOperationApplicationService.SendAsync) || args?[0] is not SendLlmChatTurnCommand command) {
                throw new NotSupportedException("This fixture owns only the native turn admission response boundary.");
            }
            Writes++;
            var operation = new LlmChatOperation(command.OperationId, command.ConversationId, LlmChatOperationKind.SendTurn,
                new(new string('a', 64)), command.ExpectedTranscriptRevision, LlmChatOperationStatus.Pending, DateTimeOffset.UnixEpoch, 0);
            var accepted = Result<LlmChatOperationDetails>.Success(new(operation, null, []));
            return Task.FromException<Result<LlmChatOperationDetails>>(new LlmChatCommittedException<Result<LlmChatOperationDetails>>(
                accepted, new IOException("PRIVATE_ADMISSION notification failure")));
        }
    }

    [Fact]
    public async Task Conversation_HTTP_preserves_native_accepted_metadata_ETag_and_warning_without_replay() {
        var service = DispatchProxy.Create<ILlmChatConversationApplicationService, ConversationOwner>();
        var owner = (ConversationOwner)(object)service;
        await using var host = await ApiTestHost.CreateAsync(false, services => services.AddSingleton(service), useInMemoryDatabase: true);
        using var created = await host.Client.PostAsJsonAsync($"/api/llm-chats/{owner.DefinitionId:D}/conversations", new { title = "Submitted title" });
        await AssertConversationAsync(created, HttpStatusCode.Created, 0);
        using var renameRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/llm-conversations/{owner.Id:D}/title") {
            Content = JsonContent.Create(new { title = "Submitted title", expectedConcurrencyToken = 0, expectedTranscriptRevision = 0 })
        };
        using var renamed = await host.Client.SendAsync(renameRequest);
        await AssertConversationAsync(renamed, HttpStatusCode.OK, 1);
        using var archived = await host.Client.PostAsJsonAsync($"/api/llm-conversations/{owner.Id:D}/archive", new LlmChatExpectedConcurrencyApiRequest(1));
        await AssertConversationAsync(archived, HttpStatusCode.OK, 2);
        Assert.Equal(3, owner.Writes);

        async Task AssertConversationAsync(HttpResponseMessage response, HttpStatusCode status, long token) {
            Assert.Equal(status, response.StatusCode);
            Assert.Equal("committed-reconciliation-pending", Assert.Single(response.Headers.GetValues(LlmChatApiResults.OutcomeHeader)));
            Assert.Equal($"\"{token}\"", response.Headers.ETag!.Tag);
            var accepted = (await response.Content.ReadFromJsonAsync<LlmChatConversationApiResponse>(Json))!;
            Assert.Equal(owner.Id, accepted.Id);
            Assert.Equal("Accepted conversation", accepted.Title);
            Assert.Equal(token, accepted.ConcurrencyToken);
            Assert.DoesNotContain("PRIVATE_SIMPLE_CHAT_COMMIT", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    public class ConversationOwner : DispatchProxy {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid DefinitionId { get; } = Guid.NewGuid();
        public int Writes { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            if (method?.Name is not (nameof(ILlmChatConversationApplicationService.CreateAsync) or nameof(ILlmChatConversationApplicationService.RenameAsync)
                or nameof(ILlmChatConversationApplicationService.ArchiveAsync))) {
                throw new NotSupportedException("This fixture owns only the native conversation metadata response boundary.");
            }
            Writes++;
            var now = DateTimeOffset.UnixEpoch.AddSeconds(Writes);
            var conversation = new LlmChatConversation(new(Id), new(DefinitionId), new(1), "Accepted conversation",
                Writes == 3 ? LlmChatConversationStatus.Archived : LlmChatConversationStatus.Active,
                LlmChatConversationOrigin.Api, DateTimeOffset.UnixEpoch, now, Writes - 1);
            var accepted = Result<LlmChatConversationDetails>.Success(new(conversation, "Owned definition",
                new(Guid.NewGuid(), "Owned provider", ProviderKind.OpenAi, "model"), new(new(Id), 0, null, now, now)));
            return Task.FromException<Result<LlmChatConversationDetails>>(new LlmChatCommittedException<Result<LlmChatConversationDetails>>(
                accepted, new IOException("PRIVATE_SIMPLE_CHAT_COMMIT")));
        }
    }
    [Fact]
    public async Task Definition_HTTP_preserves_exact_accepted_payload_ETag_and_known_warning_without_replay() {
        var service = DispatchProxy.Create<ILlmChatDefinitionApplicationService, Owner>();
        var owner = (Owner)(object)service;
        await using var host = await ApiTestHost.CreateAsync(false, services => services.AddSingleton(service), useInMemoryDatabase: true);
        var request = new LlmChatDefinitionMutationApiRequest("Submitted name", "Summary", "", "Instructions", owner.ProviderId,
            "model", null, null, [], "Owned API boundary proof");
        using var created = await host.Client.PostAsJsonAsync("/api/llm-chats", request);
        await AssertAcceptedAsync(created, HttpStatusCode.Created, owner.Id, 0);
        using var updated = await host.Client.PutAsJsonAsync($"/api/llm-chats/{owner.Id:D}", request with { ExpectedConcurrencyToken = 0 });
        await AssertAcceptedAsync(updated, HttpStatusCode.OK, owner.Id, 1);
        using var status = await host.Client.PostAsJsonAsync($"/api/llm-chats/{owner.Id:D}/activate", new LlmChatExpectedConcurrencyApiRequest(1));
        await AssertAcceptedAsync(status, HttpStatusCode.OK, owner.Id, 2);
        Assert.Equal(3, owner.Writes);
    }

    private static async Task AssertAcceptedAsync(HttpResponseMessage response, HttpStatusCode status, Guid id, long token) {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("committed-reconciliation-pending", Assert.Single(response.Headers.GetValues(LlmChatApiResults.OutcomeHeader)));
        Assert.Equal($"\"{token}\"", response.Headers.ETag!.Tag);
        var accepted = (await response.Content.ReadFromJsonAsync<LlmChatDefinitionApiResponse>(Json))!;
        Assert.Equal(id, accepted.Id);
        Assert.Equal("Accepted definition", accepted.Name);
        Assert.Equal(token, accepted.ConcurrencyToken);
        Assert.DoesNotContain("PRIVATE_SIMPLE_CHAT_COMMIT", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    public class Owner : DispatchProxy {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProviderId { get; } = Guid.NewGuid();
        public int Writes { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            if (method?.Name is not (nameof(ILlmChatDefinitionApplicationService.CreateAsync) or nameof(ILlmChatDefinitionApplicationService.UpdateAsync)
                or nameof(ILlmChatDefinitionApplicationService.ChangeStatusAsync))) {
                throw new NotSupportedException("This fixture owns only the native definition write response boundary.");
            }
            Writes++;
            var now = DateTimeOffset.UnixEpoch.AddSeconds(Writes);
            var number = new LlmChatDefinitionRevisionNumber(Writes == 1 ? 1 : 2);
            var definition = new LlmChatDefinition(new(Id), "Accepted definition", "Summary", "",
                Writes == 3 ? LlmChatDefinitionStatus.Active : LlmChatDefinitionStatus.Draft, number, DateTimeOffset.UnixEpoch, now, Writes - 1);
            var revision = new LlmChatDefinitionRevision(new(Id), number, definition.Name, definition.Summary, "", "Accepted prompt",
                ProviderId, ProviderKind.OpenAi, "Owned provider", "model", new LlmModelSettings(null, "{}"), null, null, now, "Accepted reason");
            var accepted = Result<LlmChatDefinitionDetails>.Success(new(definition, revision, []));
            return Task.FromException<Result<LlmChatDefinitionDetails>>(new LlmChatCommittedException<Result<LlmChatDefinitionDetails>>(
                accepted, new IOException("PRIVATE_SIMPLE_CHAT_COMMIT")));
        }
    }
}
