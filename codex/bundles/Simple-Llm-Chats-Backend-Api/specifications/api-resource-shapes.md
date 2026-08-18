# API resource shapes

These are semantic shapes. Transport naming may adapt to the current Web JSON conventions.

## Create/update definition request

```json
{
  "name": "Architecture assistant",
  "summary": "Helps review architecture decisions.",
  "avatarImageUrl": "/assets/avatars/architecture.png",
  "systemPrompt": "You are a careful software architecture assistant.",
  "providerProfileId": "00000000-0000-0000-0000-000000000001",
  "model": "example-model",
  "thinkingEffort": "high",
  "modelSettings": {
    "temperature": 0.2,
    "modelParameterConfiguration": {},
    "timeoutSeconds": 120
  },
  "tags": ["architecture", "review"],
  "expectedConcurrencyToken": null
}
```

The API must not accept provider credentials, endpoint, provider kind override, or a complete provider
profile. Provider kind/name are resolved and snapshotted server-side.

`thinkingEffort` is nullable. `null` means provider default; `none` is an explicit value and is accepted
only when the selected provider/model capability allows it. The API rejects any
`modelParameterConfiguration` that also supplies reasoning/thinking effort so there is one typed input
truth.

## Definition response

```json
{
  "id": "00000000-0000-0000-0000-000000000010",
  "name": "Architecture assistant",
  "summary": "Helps review architecture decisions.",
  "avatarImageUrl": "/assets/avatars/architecture.png",
  "status": "Active",
  "currentRevision": 3,
  "providerProfileId": "00000000-0000-0000-0000-000000000001",
  "providerName": "Private provider",
  "providerKind": "OpenAICompatible",
  "model": "example-model",
  "thinkingEffort": "high",
  "tags": ["architecture", "review"],
  "concurrencyToken": "00000000-0000-0000-0000-000000000020",
  "createdAtUtc": "2026-08-14T00:00:00Z",
  "updatedAtUtc": "2026-08-14T00:00:00Z"
}
```

System prompt and raw model-parameter envelope belong only in detail/editor responses, not default list
responses.

## Provider/model option response

```json
{
  "providerProfileId": "00000000-0000-0000-0000-000000000001",
  "providerName": "Private provider",
  "providerKind": "OpenAI",
  "models": [
    {
      "model": "example-model",
      "thinkingEffort": {
        "status": "Supported",
        "controlMode": "EffortLevels",
        "allowedEfforts": ["none", "low", "medium", "high"],
        "providerDefault": "medium"
      }
    }
  ]
}
```

This is a sanitized live projection of canonical provider/model capability truth, not a second stored
LLM Chat catalog.

## Create conversation request

```json
{
  "title": "Review Linux architecture",
  "origin": "Api"
}
```

The server pins the current active definition revision. The caller cannot supply another revision in
this bundle.

## Conversation response

```json
{
  "id": "00000000-0000-0000-0000-000000000030",
  "definitionId": "00000000-0000-0000-0000-000000000010",
  "definitionRevision": 3,
  "definitionName": "Architecture assistant",
  "title": "Review Linux architecture",
  "status": "Active",
  "origin": "Api",
  "transcriptRevision": 1,
  "hasActiveTurn": false,
  "createdAtUtc": "2026-08-14T00:00:00Z",
  "updatedAtUtc": "2026-08-14T00:00:00Z"
}
```

## Send turn request

```json
{
  "operationId": "00000000-0000-0000-0000-000000000040",
  "expectedTranscriptRevision": 1,
  "message": "Review this design."
}
```

No per-turn provider/model/settings override, context, or attachment payload is accepted yet. The
operation uses the immutable definition revision snapshot. DTO-local strict member validation rejects
unknown fields visibly rather than allowing the default serializer to ignore them.

## Operation response

```json
{
  "operationId": "00000000-0000-0000-0000-000000000040",
  "conversationId": "00000000-0000-0000-0000-000000000030",
  "status": "Succeeded",
  "expectedTranscriptRevision": 1,
  "resultingTranscriptRevision": 3,
  "assistantMessage": {
    "entryId": "00000000-0000-0000-0000-000000000050",
    "turnId": "00000000-0000-0000-0000-000000000040",
    "role": "Assistant",
    "content": "The design review result.",
    "model": "example-model",
    "usage": {
      "inputTokens": 100,
      "outputTokens": 40,
      "cachedInputTokens": 0
    }
  },
  "failure": null
}
```

Message content is returned only from detail/operation resources where authorization is already
enforced.
