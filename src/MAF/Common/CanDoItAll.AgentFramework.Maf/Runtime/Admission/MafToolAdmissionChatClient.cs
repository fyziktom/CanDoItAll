using System.Runtime.CompilerServices;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafToolAdmissionChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient) {
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default) {
        var admission = MafToolRunContext.Current;
        if (admission is null) {
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }

        var input = messages.ToArray();
        var digest = RequestDigest(input, options);
        var replay = await admission.ReplayResponseAsync(digest, cancellationToken);
        if (replay is not null) {
            return MafToolProtocolCodec.Decode<ChatResponse>(replay);
        }

        var response = await base.GetResponseAsync(input, options, cancellationToken);
        foreach (var message in response.Messages.Where(message => string.IsNullOrEmpty(message.MessageId))) {
            message.MessageId = Guid.NewGuid().ToString("N");
        }

        await admission.AdmitResponseAsync(digest, MafToolProtocolCodec.Encode(response),
            response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().ToArray(), cancellationToken);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        var admission = MafToolRunContext.Current;
        if (admission is null) {
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken)) {
                yield return update;
            }

            yield break;
        }

        var input = messages.ToArray();
        var digest = RequestDigest(input, options);
        var replay = await admission.ReplayResponseAsync(digest, cancellationToken);
        ChatResponseUpdate[] updates;
        var visiblePrefixCount = 0;
        if (replay is not null) {
            updates = MafToolProtocolCodec.Decode<ChatResponseUpdate[]>(replay);
        } else {
            var buffered = new List<ChatResponseUpdate>();
            var fallbackMessageId = Guid.NewGuid().ToString("N");
            var utf8Bytes = 0L;
            var deferProtocolSuffix = false;
            await foreach (var update in base.GetStreamingResponseAsync(input, options, cancellationToken)) {
                if (string.IsNullOrEmpty(update.MessageId)) {
                    update.MessageId = fallbackMessageId;
                }

                var encoded = MafToolProtocolCodec.Encode(update);
                utf8Bytes += Encoding.UTF8.GetByteCount(encoded.PayloadJson);
                if (utf8Bytes > AgentToolProtocolEnvelope.MaximumUtf8Bytes || buffered.Count >= 65536) {
                    throw new AgentToolAdmissionException("tool-admission.protocol-too-large",
                        "The complete provider response exceeds the supported admission bound; no tool was dispatched.");
                }

                buffered.Add(MafToolProtocolCodec.Decode<ChatResponseUpdate>(encoded));
                deferProtocolSuffix |= update.Contents.Any(content => content is not (TextContent or TextReasoningContent or UsageContent));
                if (!deferProtocolSuffix) {
                    visiblePrefixCount++;
                    yield return update;
                }
            }

            updates = buffered.ToArray();
            var calls = updates.ToChatResponse().Messages.SelectMany(message => message.Contents)
                .OfType<FunctionCallContent>().ToArray();
            await admission.AdmitResponseAsync(digest, MafToolProtocolCodec.Encode(updates), calls, cancellationToken);
        }

        for (var index = visiblePrefixCount; index < updates.Length; index++) {
            cancellationToken.ThrowIfCancellationRequested();
            yield return updates[index];
        }
    }

    private static AgentToolSemanticDigest RequestDigest(ChatMessage[] messages, ChatOptions? options) {
        var ordinaryOptions = options?.Clone();
        if (ordinaryOptions is not null) {
            ordinaryOptions.Tools = null;
            ordinaryOptions.RawRepresentationFactory = null;
        }

        var tools = options?.Tools?.OfType<AIFunction>().Select(tool => new {
            tool.Name, tool.Description, tool.JsonSchema
        }).ToArray();
        // The installed SDK regenerates tool-result message IDs; all three supported wire adapters use the call ID instead.
        var protocolMessages = messages.Select(message => {
            if (message.Role != ChatRole.Tool) {
                return message;
            }

            var copy = message.Clone();
            copy.MessageId = null;
            return copy;
        }).ToArray();
        return MafToolProtocolCodec.Digest(new { Messages = protocolMessages, Options = ordinaryOptions, Tools = tools });
    }
}
