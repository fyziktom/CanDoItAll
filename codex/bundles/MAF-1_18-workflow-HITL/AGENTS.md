# Codex Operating Contract

## Model and reasoning

Use GPT-5.6 with reasoning effort `xhigh`.

## Repository discipline

- Work only in the requested CanDoItAll checkout.
- Confirm the active branch and exact HEAD before editing.
- Read repository-local `AGENTS.md`, `CLAUDE.md`, build instructions, and bundle skills before action.
- Preserve unrelated work. Never use `git add .`, `git add -A`, or destructive cleanup commands.
- One subbundle equals one coherent implementation and proof unit.
- Do not commit, push, or create a pull request unless separately authorized.
- Generated bundle execution evidence may be ignored by Git; keep it locally even when it is not committed.

## Architecture discipline

For architecture-heavy C# changes, apply the repository-owned C# architecture bundle guard/governor and CodeAnalytics where available.

Prefer:

- stable abstractions owned by CanDoItAll;
- MAF-specific adapters in `*.Maf` or `*.MafAdapter` projects;
- persistence implementations in the AgentFramework module/infrastructure layer;
- typed outcomes instead of exception-driven control flow;
- explicit compare-and-set transitions;
- immutable identifiers and exact workflow-version loading;
- deterministic topology and executor identifiers;
- small composable classes rather than expanding existing large files.

Reject:

- framework types leaking into domain/API contracts;
- a second competing workflow runtime manager;
- a second external-request API;
- `Task.Run` or fire-and-forget resume;
- in-memory-only checkpoint state for an API-visible production flow;
- client-supplied actor identity;
- broad “catch and restart” recovery;
- fake separation implemented only with partial classes;
- tests that mock away the MAF checkpoint and response protocol.

## Tool execution policy

- `AllowConcurrentInvocation` remains false by default.
- Do not infer safety from tool read/write names alone.
- Never enable concurrency for tools with ordering dependencies, approvals, mutable shared state, file writes, process execution, database writes, project mutations, or external side effects.
- Do not expose a public concurrency toggle in this bundle.
- `ChatOptions.AllowMultipleToolCalls` and MAF `AllowConcurrentInvocation` are separate concepts.
- If an application-owned `FunctionInvokingChatClient` already has concurrency enabled, treat that as a blocker and investigate before changing package versions.

## Workflow HITL policy

- Native MAF request/response and checkpoint APIs are the execution mechanism.
- CanDoItAll owns public IDs, authorization, audit, response-operation state, payload policy, and persistence abstractions.
- MAF checkpoint payloads are opaque implementation data behind a CanDoItAll port.
- Resume must rebuild the exact saved workflow definition version and verify a topology fingerprint before invoking MAF.
- A missing or incompatible checkpoint is a typed terminal or recoverable failure, never a request to rerun from the beginning.
- Response acceptance is exactly once at the API/domain boundary.
- Runtime continuation is replayable; side-effecting executors must use a stable deduplication key.
- An approval denial is a valid governed outcome, not an unhandled exception.

## Validation discipline

- Start with the narrowest stable project and `FullyQualifiedName` filter.
- Record actual discovered count. Zero discovered tests is failure.
- Run tests after the owning code change, not after every file edit.
- Do not repeatedly run UI or full-solution tests.
- Run the broad build/test gate once at the named frozen checkpoint in SB06, after all focused checks pass.
- When a check cannot run, record the exact reason and the next-best proof.
- All comments added to source code must be in English.

## Reporting

Update:

- root `STATUS.md`;
- the active subbundle closure record;
- `traceability/TRACEABILITY.md`;
- `closeout/EXECUTION-REPORT.md` when a phase closes.

Report facts, commands, discovered counts, outcomes, and remaining risk. Do not replace missing proof with confidence language.
