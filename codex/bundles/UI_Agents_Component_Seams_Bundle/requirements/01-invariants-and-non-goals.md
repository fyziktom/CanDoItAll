# Invariants and non-goals

## Compatibility invariants

- current recognized query state must build the same canonical current URL;
- current unknown/obsolete tab behavior remains Overview fallback;
- no deep link may silently select or edit a different agent;
- current default editor section remains Identity;
- section order and labels remain unchanged;
- current notifications/dialog results remain semantically equivalent;
- no mutation occurs before the existing confirmation point;
- failures retain current retry/open-editor behavior;
- data-test IDs used by the focused tests and host smoke remain stable unless the old ID
  is proven misleading and every consumer is migrated in the same subbundle.

## Architecture invariants

- one semantic state owner;
- controllers expose workflows/results, not every underlying service method;
- controllers do not know Razor component instances, `RenderFragment`, URLs, or dialog
  presentation;
- `AgentCatalogPanel` is not wrapped by another Razor container;
- `AgentDetailsDialog` remains a feature editor, not a generic form framework;
- no direct persistence or service location in Razor;
- no new project reference or `AppComponents` feature dependency;
- no additional partial file.

## Non-goals

- no canonical route migration;
- no agent detail page;
- no routed overlay host;
- no provider workspace refactor;
- no CSS redesign or responsive expansion;
- no sandbox application;
- no project extraction;
- no build/watch performance claim;
- no cleanup of unrelated tests or modules;
- no replacement of every component DI dependency across AgentFramework.
