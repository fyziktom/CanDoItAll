# C# Architecture Gate

Update at CP1, CP2, CP3, and FINAL.

## Snapshot And Health

- Snapshot id: `snap-20260816214112-d26d371e` at CP1; fresh, not cached, no blocking errors.
- Scoped projects/namespaces: `CanDoItAll.Conversations.Components`, `CanDoItAll.AgentFramework.Components`, `CanDoItAll.Modules.AgentFramework`, `CanDoItAll.Modules.LlmChats`, `CanDoItAll.Modules.LlmChats.Persistence`, and `CanDoItAll.Web`.
- Diagnostics: 20 non-blocking analyzer diagnostics; no snapshot warning or blocking error.
- Findings/hotspots: 344 risk findings and 18 open questions retained for later checkpoints; no project-boundary regression in the SB02-SB04 diff.

## Dependency Direction

- New references: none in SB05. The scoped snapshot keeps `Conversations.Components` backend-neutral, `AgentFramework.Components -> Conversations.Components`, `Modules.AgentFramework -> AgentFramework.Components`, `Modules.LlmChats.Persistence -> Modules.LlmChats`, and Web as composition root.
- Reverse references: none introduced.
- Cycles: no new cycle. Snapshot structure hash `d26d371e` matches the pre-SB05 baseline. The two known AgentFramework module/type cycles remain unchanged and are not touched by this checkpoint.
- Forbidden references scan: pass; the neutral conversation component project has no project references and Simple Chat route/catalog activation is absent before CP1.

## Responsibility Movement

- Old owner before: Agent presentation remains in `AgentFramework.Components` and `Modules.AgentFramework`; shared transcript primitives remain in `Conversations.Components`.
- New owner after: unchanged at CP1; this checkpoint activates no feature owner.
- Old owner shrink/thin-facade proof: not applicable because SB05 changes no production source.
- No-new-partial proof: `git diff 9d806df..9d806df` is empty for production source.

## Pattern Adequacy

- Adapter/gateway/reducer/contributor decisions followed: SB02-SB04 contracts remain typed and boundary-owned; CP1 adds no adapter or gateway.
- Rejected shortcuts absent: no Simple Chat component, route, navigation item, backend reference, duplicate renderer, or stringly-typed activation was added before the gate.

## Independent Testability

- Types/components tested without old runtime: 1,007 Component tests and 6,229 Unit tests pass in their isolated workspaces.
- Negative shallow-separation test: existing component and operation/profile fence tests pass; the pre-CP1 `/chats` request returns 404 and the contextual agent catalog excludes agents without access.
- Composition smoke: real managed Web app at 1600x1000 passes Agent catalog, settings, main-chat, floating lifecycle, and Project Structure context smoke.

## Verdict

- `Pass` at CP1.
- Downstream unlock: set `simpleChatUiActivationAllowed=true` and unlock SB06. Keep floating Simple Chat integration locked until CP2.
