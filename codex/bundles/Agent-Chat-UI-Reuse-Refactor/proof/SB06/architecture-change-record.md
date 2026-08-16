# Architecture change record — SB06

## Decision

Definition-editor presentation now belongs to `CanDoItAll.Conversations.Components`. `AgentDetailsDialog` remains the Agent application owner and consumes the neutral editor through explicit bindings and render slots.

## Responsibility moved

- editor shell composition and validation/action slots
- identity, avatar, name, optional role, summary, and instructions presentation
- configurable field labels and direct disabled/validation behavior
- opaque provider option presentation and availability state
- provider default, suggested model, and explicit custom-model selection behavior
- optional neutral temperature field and advanced-settings slot

## Responsibility retained by AgentFramework

- `ProviderProfile` and `Guid` identity mapping
- Agent validation, persistence, optimistic versioning, save, delete, and dialog lifecycle
- avatar choose/default/generate orchestration and tag editing
- reasoning effort, runtime parameter policy, approvals, access policies, capabilities, voice, memory, images, project structure, workspace tools, and secrets
- every Agent-only tab and all service calls

## Dependency direction

`CanDoItAll.Modules.AgentFramework` -> `CanDoItAll.AgentFramework.Components` -> `CanDoItAll.Conversations.Components`.

The neutral layer receives `ConversationPresentationKey` and `ConversationProviderOption`; it does not reference `ProviderProfile`, Agent definitions, LlmChats, backend services, persistence, EF, or service location. Scoped CodeAnalytics snapshot `snap-20260816125825-acdf4779` confirmed three projects, the expected direct references, and no new project cycle or blocking diagnostic (`code-analytics_d9750253bd684414bbd2bdf0a83726e3`). Broad pre-existing module/type-cycle findings were unrelated to the changed boundary.

## Architecture review

- `ProviderModelSelector` is now a thin compatibility facade; default/suggested/custom selection behavior has a single neutral owner
- `AgentDetailsDialog` lost identity/provider presentation markup but retained application and runtime ownership
- the editor exposes focused render slots instead of Agent flags or a boolean-god contract
- no new partial file or service-locator dependency was introduced
- the neutral temperature component is prepared for reuse but is not activated in Agent UI, avoiding a product behavior change

Decision: behavioral architecture gate passes to SB07. This subbundle closes no named architecture checkpoint.
