# Shared execution preamble

You are executing the CanDoItAll Agent Chat UI Reuse Refactor Phase 1 bundle.

Before editing:

1. read the root README, status, manifest, current subbundle, architecture decisions, requirements, traceability, and source evidence;
2. load the current SharedInfo skills named by the root README and record their hashes;
3. reconcile the live `simple-chats` branch against preparation head `eca249942211d9d8839f3e0da9b1997b7d652684`;
4. build/reuse the narrowest healthy CodeAnalytics snapshot for the current responsibility family;
5. inspect exact source definitions, consumers, project references, CSS, and nearby tests;
6. run the subbundle entry validator;
7. stop for repair when prerequisites or source ownership materially differ.

During implementation:

- preserve current Agent behavior;
- keep backend effects in Agent-owned code;
- keep neutral components free of Agent and LlmChats types;
- do not add product-kind switches, service location, or partial-class expansion;
- use Components MCP before choosing/refactoring structural composition;
- use the actual diff and changed line ranges for impacted tests;
- record proof while fresh.

At closure:

- run every required selector with nonzero discovery;
- run source/dependency/phase guards;
- update proof, status, and progression;
- stop at the declared checkpoint;
- do not start the next subbundle when the checkpoint is blocked or awaiting user action.
