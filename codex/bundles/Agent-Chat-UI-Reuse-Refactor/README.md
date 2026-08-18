# CanDoItAll Agent Chat UI Reuse Refactor — Phase 1

## Status

**Prepared implementation bundle.**

This bundle does not implement Simple Chat UI. It refactors the current Agent Chat UI into a backend-neutral, application-owned presentation boundary while keeping all current Agent behavior intact.

Execution starts from:

- repository: `fyziktom/CanDoItAll`
- branch: `simple-chats`
- preparation-time head: `eca249942211d9d8839f3e0da9b1997b7d652684`
- parent bundle commit: `c3c7713927b9519200900583f227ead95fafb5e9`
- preparation-time SharedInfo head: `7b7808e8591d7219f40826cf0e5624e182981d90`

At execution time, Codex must reconcile the live branch and current SharedInfo skills before editing. Preparation-time hashes are evidence, not permission to ignore newer instructions.

## Intended outcome

After all subbundles:

1. current Agent Chat pages, floating windows, contextual windows, Process consumers, settings, list/picker surfaces, approvals, voice, attachments, prompt gallery, runtime details, and execution behavior still work as before;
2. reusable conversation presentation is owned by a backend-neutral UI boundary;
3. agent domain models and services are mapped by agent-owned adapters;
4. no production UI consumes `CanDoItAll.Modules.LlmChats`;
5. no mixed Agent/Simple Chat catalog, filter, route, context button, or SSE client is added;
6. execution stops in `awaiting-user-agent-chat-regression`;
7. the user manually tests Agent Chats before a separate Simple Chat UI bundle is prepared.

## Preferred boundary

Create:

`src/UI/CanDoItAll.Conversations.Components/CanDoItAll.Conversations.Components.csproj`

The project should contain only application-owned conversation presentation contracts, Razor components, safe markdown rendering, and isolated UI helpers.

Allowed dependencies should remain minimal:

- `Microsoft.AspNetCore.Components.Web`
- `CanDoItAll.Components.BaseLib`
- `Markdig` only if the neutral renderer owns markdown conversion
- another source-neutral UI package only when the Components MCP and dependency review prove it is required

Forbidden dependencies include:

- `CanDoItAll.AgentFramework.Models`
- `CanDoItAll.AgentFramework.Core`
- `CanDoItAll.AgentFramework.Voice`
- `CanDoItAll.Modules.AgentFramework`
- `CanDoItAll.Modules.LlmChats`
- EF Core, `AppDbContext`, persistence, provider SDKs, host services, and runtime orchestration

A fallback under `src/UI/CanDoItAll.AppComponents/Components/Conversations` is allowed only when CP1 records CodeAnalytics evidence that the focused project is invalid or cyclic. Convenience is not a fallback reason.

## Execution order

| Order | Subbundle | Outcome | Proof tier |
|---:|---|---|---|
| 1 | SB01 | Freeze source, architecture, consumers, tests, and rendered parity | Governed |
| 2 | SB02 | Create the neutral Conversation Components boundary | Governed |
| 3 | SB03 | Extract participant cards, lists, and picker presentation | Behavioral |
| 4 | SB04 | Extract thread rail, items, search, and history presentation | Behavioral |
| 5 | SB05 | Extract workspace, transcript, markdown, and composer presentation | Governed |
| 6 | SB06 | Extract reusable definition/settings field groups | Behavioral |
| 7 | SB07 | Extract floating catalog and lifecycle-settings seams | Behavioral |
| 8 | SB08 | Migrate all existing agent consumers and close architecture | Governed |
| 9 | SB09 | Run focused regression proof and hand off to the user | Governed |

Do not execute subbundles in parallel. Later subbundles depend on source and proof produced by earlier ones.

## Mandatory skill posture

Before execution, load and follow the current versions of:

- `apply-candoitall-shared-standards`
- `bundles/candoitall-bundle-execution`
- `bundles/candoitall-subbundle-validator`
- `bundles/candoitall-bundle-validator`
- `candoitall-codeanalytics-mcp`
- `candoitall-components-mcp`
- `candoitall-frontend-theme`
- `csharp-architecture-governor`
- `csharp-project-boundary-extraction`
- `csharp-dependency-graph-audit`
- `csharp-modular-refactoring`
- `csharp-testability-contracts`
- `csharp-architecture-review-gate`

Use Playwright/browser proof only at named checkpoints and for the final focused regression pass. Do not repeatedly launch broad UI suites.

## Focused test rule

For every production-changing subbundle:

1. derive the actual changed files and one-based changed line ranges from the subbundle diff;
2. call `code_analytics_impacted_tests_get` with `behaviorIntent=Unknown`;
3. put inspected but unchanged files in `contextOnlyPaths`;
4. verify workspace health and nonzero source/test discovery;
5. run every required selector and verify nonzero test discovery;
6. promote conditional selectors only when their returned trigger occurs;
7. call again with `BehaviorPreservingImplementation` only when that assertion is justified by the conservative result and the actual change;
8. do not run unfiltered solutions by habit.

An unfiltered Stable/full gate is allowed at most once in SB09 and only when the impacted-test result or a recorded invalidation trigger requires it.

## Hard phase exclusions

This bundle must not add or activate:

- Simple Chat pages, menus, routes, dialogs, cards, filters, or floating windows
- direct `CanDoItAll.Modules.LlmChats` references from UI
- mixed Agent/Simple Chat catalogs
- Agents/Simple Chats filter controls
- Simple Chat API or SSE clients
- the future **Add context** button
- project-structure or selected-node/subtree context capture for Simple Chats
- Simple Chat dependency injection registrations
- changes to Simple Chat backend behavior, persistence, transport, or schema
- a source-switch component containing branches such as `if agent ... else simple chat ...`

Future seams may be documented, but they must remain unregistered and unused.

## Completion decision

Phase 1 is complete only when:

- CP0–CP5 are closed;
- bundle and source guards pass;
- all required impacted-test selectors ran with nonzero discovery;
- focused desktop browser proof is inspected;
- any broad gate was explicitly triggered and ran at most once;
- final status is `awaiting-user-agent-chat-regression`;
- the user receives the manual regression checklist.

Do not mark the branch `ready-for-simple-chat-ui` inside this bundle.
