# Semantic adequacy review

## Question

Does the bundle describe the requested outcome rather than only a project rename?

## Answer

Yes.

- Logical consolidation: provider setup remains canonical in AgentFramework and the Agent page becomes the product surface.
- Physical isolation: Core, Application, Runtime, Persistence, and Components remain reusable MAF libraries.
- Cost consolidation: neutral typed analytics composes both authoritative stores with exact attempt and pricing semantics.
- UI consolidation: Simple Chats is next to Agents, not a second module/page.
- Compatibility: APIs, scopes, tables, migrations, transfer, profile fences, streaming/recovery, and floating behavior are preserved.
- Closure: named main/floating Agent and Simple Chat E2E plus all cost scopes are mandatory.

## Ambiguities resolved

- “Both” is a query selection, never a persisted workload kind.
- Simple Chat invocation attempts, not transcripts, are the cost source.
- Historical rows are not repriced.
- Agent catalog counts do not change with usage scope.
- Chats are not displayed as Agents; consumer identity stays typed.
- Existing generic AgentFramework.Llm.* projects satisfy the basic abstractions/helper requirement; the feature adds Core/Application rather than another empty Abstractions project.
- /chats remains redirect compatibility, while /agents?tab=simple-chats is canonical.

## Remaining execution-time decisions

CP0 may adjust exact type names or query key spellings only when it preserves the semantics above and updates requirements/tests before implementation. It may not collapse target layers, introduce a central ledger, or guess historical cost without returning the bundle for review.

