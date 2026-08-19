# Provider streaming contract

## Provider-neutral surface

Add an additive contract such as:

```csharp
public interface ILlmStreamingInvocationPort
{
    IAsyncEnumerable<LlmStreamingUpdate> StreamAsync(
        LlmInvocationRequest request,
        CancellationToken cancellationToken = default);
}
```

The final names may follow repository conventions. Updates must be a closed typed hierarchy or
discriminated record set covering:

- attempt started;
- text delta;
- optional reasoning/metadata delta only if explicitly supported and safe;
- completed with model, usage and finish reason;
- typed failure.

No provider SDK/wire types cross the port.

## Provider capability

Add `IProviderStreamingChatCompletionDriver` beside the completed driver. A provider may support:

- true streaming;
- completed fallback;
- unsupported.

Capability resolution must be explicit per provider/model.

## Retry rules

- Retry is allowed only before any accepted text delta.
- Each dispatch attempt receives a real ordinal and audit row.
- Known usage from failed attempts is preserved.
- After first delta, transport/provider failure is terminal or RecoveryRequired; no silent splice.
- Empty completed response uses one documented policy shared by streaming and non-streaming paths.

## Backpressure and bounds

The port must not buffer the entire answer before yielding. The product pipeline may coalesce small
deltas by byte/time thresholds, but enforces:

- maximum accumulated assistant characters/bytes;
- maximum event count and event payload;
- maximum operation duration;
- cancellation/profile lifetime checks.

## Driver responsibilities

OpenAI/Azure and Ollama drivers parse their own framing, validate terminal markers, aggregate usage and
redact raw failures. HTTP/SSE transport does not parse provider protocols.
