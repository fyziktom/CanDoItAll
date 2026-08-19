# Future Simple Chat seams — documentation only

The Phase 1 neutral boundary should make the following later mappings possible without implementing them now:

| Future Simple Chat field/behavior | Neutral seam |
|---|---|
| definition id | opaque participant key |
| name | display name |
| summary | summary/subtitle |
| avatar URL | avatar presentation |
| active/suspended status | supplied badge |
| system prompt | configurable instructions field label |
| provider/model | neutral provider/model options |
| temperature | optional neutral numeric field |
| thinking effort | advanced settings option/slot |
| conversation list | neutral thread list |
| transcript | neutral message presentation |
| streamed partial response | parameter updates to transcript/message content; no SSE client in Phase 1 |
| context attachment | explicit context/adornment/composer action slot; no project context behavior in Phase 1 |
| floating catalog entry | neutral participant presentation; no mixed catalog in Phase 1 |

Do not add `SimpleChat`, `LlmChatDefinition`, `ILlmChat*`, API clients, or SSE transport types to the neutral project to prove these seams.
