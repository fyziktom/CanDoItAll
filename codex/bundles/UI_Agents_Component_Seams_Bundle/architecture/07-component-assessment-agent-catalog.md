# Component boundary assessment — AgentCatalogPanel

## Identity

- feature-owned Agents catalog component;
- remains in current module and file location;
- future destination: `CanDoItAll.Modules.AgentFramework.UI`, not `AppComponents`.

## Rendering responsibility

Search, team/agent tree, selected-team heading, agent cards, action affordances, loading,
empty, and error presentation.

## Current non-rendering responsibility to remove

- organization catalog repair;
- agent/team/provider load and reload;
- provider privacy mapping;
- selected/requested reconciliation owned jointly with page;
- direct agent/team dialogs;
- team member/delete mutations;
- managed chat launch;
- notifications;
- private open-echo suppression.

## Target public contract

```text
AgentCatalogViewState State
EventCallback<AgentCatalogIntent> IntentRequested
```

Existing separate callbacks/initial-data parameters may be removed after all production
and test callers migrate. Do not retain both old and new state machines as a permanent
compatibility layer.

## Local state retained

- search text;
- expanded tree nodes;
- purely visual/interaction state.

## Target injected dependencies

None.

## Acceptance

- component renders with an ordinary bUnit context plus BaseLib only;
- card/team actions emit correct intents;
- no DialogService, NotificationService, chat launcher, Workspace, provider, or repair
  dependency;
- requested-agent open-once behavior is proven at the page boundary;
- no wrapper Razor component and no new partial file.

## Readiness after bundle

- route-ready: yes through page-owned selection/detail intents;
- sandbox-ready: yes at component level with explicit state;
- project-extraction-ready: yes logically, pending physical project split and CSS/assets.
