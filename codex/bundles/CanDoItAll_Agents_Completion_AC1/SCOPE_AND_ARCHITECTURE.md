# Completion architecture

## Placement

The recommended destination for Agents shell, Usage, runtime details and compact conversation adjuncts is the **existing** `src/UI/CanDoItAll.AgentFramework.UI` and `src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox`. Its project already references Usage, Models, History abstractions and neutral Conversations/BaseLib/Charts (S22). This is a starting preference, not authorization to drag Core/Voice/persistence through its closure.

Use a new narrowly named leaf only when evaluated dependencies demonstrate that it keeps meaningful boundaries independent. Do not create another monolithic all-Agents UI aggregator. Completed editor, provider, Sharing, History, Workflow and Workspace leaves must not gain dependencies on one another's native implementations. Existing MAF Components may retain native integration adapters and compatibility hosts; a shared rendering library must never reference that broad adapter assembly just to get one dialog.

Production direction: Web/composition -> native module/effect host -> actual feature renderer -> existing light value contracts and neutral rendering. Sandbox -> same renderer + deterministic values/delegates. No EF, application service collection, `IServiceProvider`, driver/vault/store, framework runtime or production startup inside the sandbox.

## Real state owners

| State/effect | Owner |
|---|---|
| Public route, tab token, selected agent/team/context readiness | Existing routed/native host |
| Usage applied query/window and allowed read context | Existing query owner and bounded per-view read session |
| Charts/grids/status, immutable view values, focus/disclosure | Actual renderer |
| Durable run/chat, approvals, tool effects, retry and cancellation | Current native runtime/admission services |
| Affinity following/detachment and context lease | Current conversation context service/coordinator |
| Explicit Voice save, synthesis, playback and settings scope | Existing host/services, with distinct outcome/lifetime |
| Native execution log redaction/credential disclosure policy | Existing policy boundary; renderer receives safe values or audited light pure formatter |
| Scenario selection | Sandbox only |

Choose presentation+intent for read views and small strips; a cohesive view/operations contract is acceptable for a complex stateful surface. No per-button interface quota. Preserve raw drafts and asynchronous owner checks, including errors, notifications and finally. Do not serialize runtime scopes, transient requests or sensitive content into navigation.

The native Scheduler fix is a separate bounded owner correction. It does not make Scheduler an Agents rendering dependency or justify moving its DbContext/Quartz services into UI. Likewise the Workflow result-attribution repair must preserve the independent Workflow authoring leaf.

Use current non-WebGL Components MCP when available, otherwise inspect actual component contracts and report the fallback. No new Radzen. Move corresponding scoped CSS/JS/Tailwind inputs and published assets with the renderer. Do not claim completion based only on moved `.razor` paths or project count.
