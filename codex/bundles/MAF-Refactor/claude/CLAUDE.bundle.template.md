# CanDoItAll architecture bundle execution rules

> Merge only the missing parts of this template into the repository's existing `CLAUDE.md`. Do not replace established repository instructions.

## Bundle

- Execute one subbundle at a time from `CanDoItAll-AgentRuntime-Context-MAF-Refactor-Claude-Fable5-Bundle-v2`.
- Read the selected subbundle `CLAUDE-CODE-PROMPT.md` and README before editing.
- Respect checkpoint unlock decisions.
- Keep proof and session handoff files current.


## Optional bounded imports

When the bundle is copied inside the repository, replace `<bundle-relative-path>` and add only stable root imports to the existing `CLAUDE.md`:

```text
@<bundle-relative-path>/00-READ-ME-FIRST.md
@<bundle-relative-path>/04-CLAUDE-CODE-EXECUTION-GUIDE.md
@<bundle-relative-path>/sharedinfo/required-skills.md
```

Do not import every subbundle or architecture document globally. Pass the active subbundle prompt explicitly so stale/later-phase instructions do not consume context or override checkpoint order.

## Architecture

- Contracts and abstractions are SDK-free.
- Core/application does not reference MAF, product UI modules, persistence implementations, or provider SDKs.
- MAF is an adapter and cannot own process semantics or product authority.
- UI context is observation, never authorization.
- Approval continuation uses the original turn context and authority.
- Every execution uses one coherent workspace scope/service bundle.
- Runtime/core behavior must not retain `IServiceProvider` or perform service location.
- No new partial-class architecture, nested architecture owner, broad helper/manager, or Common dumping ground.
- Lightweight LLM invocation must not construct agents, sessions, tools, memory, handoffs, approvals, finalizers, or product context.

## Workflow

- Use CodeAnalytics MCP for scoped orientation and dependency proof when available.
- Inspect exact source and `.csproj` files before edits.
- Add characterization/failing-first tests before moving behavior.
- Make small buildable cutovers and run focused tests after each.
- Never shadow-execute side-effecting provider/tool/process paths.
- Fix bugs in the owning layer and add a regression test first.
- Do not commit or push unless explicitly requested.

## Source code

- All source-code comments must be in English.
- Preserve nullable correctness, cancellation, disposal, structured logging, and existing public behavior unless the active subbundle explicitly changes it.
