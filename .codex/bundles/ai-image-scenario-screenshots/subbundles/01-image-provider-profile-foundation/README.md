# image-provider-profile-foundation

## Status

- `Completed`

## Objective

Add typed image-generation provider support and typed per-agent image-tool preference so image generation can be allowed, selected, and validated without process-core special cases.

## Success Criteria

- OpenAI image generation provider profile is seeded and visible through provider catalog/editor/API paths.
- `gpt-image-1-mini` is the configured default image model unless a provider-validation step proves it unavailable and records the replacement.
- Agent configuration metadata can express image generation allowed/disallowed, preferred image provider ID, default image model, and project-asset storage permission.
- Existing text/chat provider behavior remains compatible.

## Covered Inputs

- R1, R2, R3.
- Raw notes `N001`, `N002`, and the process-core genericity constraint from `N007`.

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Providers\ProviderModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Common\Enums.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\AgentModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\Access\AgentWorkspaceToolAccessModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedBuilder.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedNormalizer.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\AgentsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Models\WorkspaceModels.cs`

## Deliverables

- Typed image provider/profile model additions or a clearly bounded image-provider subtype.
- OpenAI image provider seed profile using `OPENAI_API_KEY`.
- Agent image access/preference metadata read/write helper modeled after existing access metadata helpers.
- Catalog/editor/API mapping for the new metadata where required.
- Focused unit tests for metadata serialization and provider seed normalization.

## Dependency Impact

- Subbundles 04 and 06 depend on this phase to resolve preferred image providers without parsing natural-language prompts.
- Weak proof here invalidates screenshot-review storage agents and layout-generation agents.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Inspect existing provider profile, provider-kind, editor, registry, and seed normalization paths.
2. Add the smallest typed model changes required for image-generation providers and preferred provider metadata.
3. Seed the OpenAI image provider with `OPENAI_API_KEY`, OpenAI base URL, and `gpt-image-1-mini`.
4. Add agent metadata helpers for image-generation access and preferred provider selection.
5. Wire read/write mapping into agent catalog/editor/API surfaces only where the existing code requires explicit mapping.
6. Add focused tests for metadata round-trip and seed normalization.
7. Run targeted build/tests and update `reviews/01-execution-report.md`.

## Scope Exceptions

- Do not implement ComfyUI runtime calls in this phase; only keep the provider model extensible enough to add it later.
- Live OpenAI image generation can be deferred to subbundle 06.

## Do Not Do

- Do not add screenshot, route, Playwright, or image-generation branches to process runtime.
- Do not hide missing credentials with a fallback provider.
- Do not replace the existing text/chat provider model wholesale.

## Acceptance Checklist

- [x] Provider model supports image-generation profiles.
- [x] OpenAI image-generation provider is seeded.
- [x] Agent metadata round-trips typed image access and preferred provider settings.
- [x] Existing providers still load.
- [x] Targeted tests pass.

## Proof Required

- `dotnet test` for the relevant agent-framework model/persistence tests, or the narrowest available test project that covers seed normalization.
- Build proof for touched projects if no focused tests exist.
- Provider/agent API readback proof when the web app is running.

## Browser Validation Logging

- N/A for browser UI unless provider/agent editor UI is touched.

## Progression Gate

- Downstream subbundles may proceed only after provider and agent metadata have typed read/write proof.
- The diff must show no process-core screenshot/image-generation special casing.

## Suggested Agent Prompt

```text
Implement only the image-provider-profile-foundation subbundle.
Add strongly typed image-generation provider and agent image-access metadata with OpenAI as the first seeded provider. Keep process core generic. Do not implement screenshot or layout processes yet. Capture build/test proof and update the execution report.
```
