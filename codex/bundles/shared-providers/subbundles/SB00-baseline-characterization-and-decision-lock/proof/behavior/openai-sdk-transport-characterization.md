# SB00 OpenAI transport characterization

Captured: 2026-08-24
Production behavior changed: **No**

## Scope and distinction

`MafProviderAgentFactory` builds the ordinary-agent OpenAI path with the repository's pinned
`OpenAI` package (`2.12.0`). For a non-default provider endpoint it assigns the validated
`ProviderProfile.BaseUrl` to `OpenAIClientOptions.Endpoint`, then selects either
`GetChatClient(model)` or `GetResponsesClient()` from the profile transport.

The focused SB00 integration test now maps the canonical Workspace profile and gives that mapped
custom endpoint to an actual `OpenAIClient` using an in-process capture transport. It does not
substitute a hand-written route joiner for the SDK. The separate Images assertion remains against
the production `OpenAiProviderDriver`, because image execution is a different current runtime
path.

## Observed wire behavior

The source endpoint `https://relay.example.test/custom/v1` has no trailing slash. Four actual SDK
operations preserve the complete base path:

| SDK operation | Streaming | Captured request path |
| --- | --- | --- |
| Chat Completions | no | `/custom/v1/chat/completions` |
| Chat Completions | yes | `/custom/v1/chat/completions` |
| Responses | no | `/custom/v1/responses` |
| Responses | yes | `/custom/v1/responses` |

All four calls send the SDK client's bearer credential; provider-scoped credential resolution is
separately characterized by the runtime-path evidence. Streaming calls serialize `stream: true`
and consume provider-shaped SSE into public text. The Chat stream surfaces its terminal finish
reason. With the pinned SDK, the Responses stream yields its typed text delta and completes after
the terminal event without yielding a separate typed completion update; the test locks that
observed behavior instead of claiming otherwise. Non-streaming and streaming replies are both
deserialized by the actual SDK. The production image driver independently preserves the same
custom prefix as `/custom/v1/images/generations`.

This closes the prior evidence gap: mapper preservation alone did not prove SDK route composition,
Chat Completions, Responses, or either streaming surface.

## Durable test anchor

- `repo://tests/Integration/CanDoItAll.Tests.Integration/SharedProviderRuntimePathCharacterizationTests.cs`
- Exact class filter: `FullyQualifiedName~SharedProviderRuntimePathCharacterizationTests`
- Discovery: **6** tests (planned count preserved)
- Execution: **6 passed, 0 failed, 0 skipped**
- Focused duration reported by VSTest: **233 ms**

Durable transcripts:

- `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-build-integration-sdk-transport.txt`
- `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-list-integration-sdk-transport.txt`
- `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-run-integration-sdk-transport.txt`

Commands used:

```text
dotnet build tests/Solutions/CanDoItAll.Tests.Integration.slnx --configuration Release --no-restore /m:1 -nologo --verbosity minimal
dotnet test tests/Solutions/CanDoItAll.Tests.Integration.slnx --configuration Release --no-build --no-restore --list-tests --filter FullyQualifiedName~SharedProviderRuntimePathCharacterizationTests /m:1 -nologo --verbosity minimal
dotnet test tests/Solutions/CanDoItAll.Tests.Integration.slnx --configuration Release --no-build --no-restore --filter FullyQualifiedName~SharedProviderRuntimePathCharacterizationTests /m:1 -nologo --verbosity minimal
```

Final build result: **0 warnings, 0 errors**. The test uses only deterministic in-process HTTP
responses and makes no live provider call.

## Reopen triggers

- `MafProviderAgentFactory` stops assigning the mapped non-default endpoint to the OpenAI SDK.
- The pinned OpenAI SDK version changes.
- The shared projection changes the endpoint root supplied to MAF.
- Chat Completions or Responses route composition changes.
- Streaming is moved to a transport that does not use these SDK surfaces.
