# Negative and source guards

## Historical negative

At the proven SB06 head `3159b4c`, the following source lookup is empty:

```powershell
git grep -n "ILlmStreamingInvocationPort\|IProviderStreamingChatCompletionDriver\|ProviderBackedLlmStreamingInvocationAdapter" 3159b4c -- src
```

Result: expected red. The previous implementation had no provider-neutral streaming owner and could
only obtain a completed response before returning text. The new fragmented-frame and
no-retry-after-delta tests therefore fail against that shallow boundary by construction.

## Current assertions

- `ILlmInvocationPort.InvokeAsync` is unchanged and its existing compatibility suite remains green.
- `ILlmStreamingInvocationPort` and its update hierarchy reference Models only; no Web, SSE, Razor,
  ASP.NET Core, or provider wire type enters the abstraction.
- `IProviderStreamingChatCompletionDriver` resolves through the existing ChatCompletion capability;
  OpenAI, Azure OpenAI, and Ollama implement it directly.
- OpenAI/Azure send `stream=true` and parse their own SSE protocols; Ollama sends `stream=true` and
  parses its own NDJSON protocol.
- `retryScheduled = !emittedDelta && ...`; the partial-output test proves one provider call and a
  terminal failure after the first delta.
- completed-only support emits a single delta and completion with `CompletedFallback`.
- no new production partial class exists and the search found no transport/Web dependency in the
  LLM contracts or runtime adapter.

## Anti-stub audit

Run label: SB07 closure. Working directory: `C:\repositories\CanDoItAll`. Exit: 0.

```powershell
Select-String -Path <scoped production streaming files> -Pattern 'TODO|FIXME|NotImplementedException|fixture-specific|test-only|stub'
```

Result: `ANTI_STUB_PASS`. No marker exists in the production streaming path. The new path performs
real provider-response enumeration and runtime dispatch; there is no template-only output,
fixture-conditioned production branch, or manual-only producer.
