# Tool Concurrency Policy for MAF 1.18

## Decision

CanDoItAll will remain serial for tool invocation after the 1.18 upgrade.

MAF's new concurrent invocation support is an available capability, not a safe default. The model may still request multiple tools in one turn; CanDoItAll executes them sequentially unless a future governed scheduler proves that a specific set is independent.

## Required configuration

After restoring 1.18, the application-owned `ChatClientAgentOptions` construction must explicitly set:

```csharp
AllowConcurrentInvocation = false
```

The exact property must be verified against the restored 1.18 assembly.

This setting belongs in the central options factory and any bypassing construction path discovered by SB00.

## Required composition audit

Search for:

- `new ChatClientAgentOptions`
- `AllowConcurrentInvocation`
- `FunctionInvokingChatClient`
- `UseProvidedChatClientAsIs`
- `AsAIAgent`
- `IChatClient` decorators
- custom tool invocation middleware
- provider-specific client factories

Decision rules:

1. When CanDoItAll lets MAF construct the invocation pipeline, central false is authoritative.
2. When `UseProvidedChatClientAsIs = true`, inspect the provided client stack; do not assume the agent option applies.
3. When an existing `FunctionInvokingChatClient` has concurrency enabled, stop SB02 and establish why before changing it.
4. A provider's `AllowMultipleToolCalls` or equivalent does not authorize concurrent execution.
5. Do not disable a provider's ability to return multiple calls merely to enforce serial execution; preserve the calls and execute them in order.

## Why serial is necessary today

Order matters for common CanDoItAll operations:

- create directory before write file;
- read current version before conditional update;
- acquire approval before mutation;
- create project item before linking it;
- load credentials before external call;
- start process before inspect output;
- write artifact before register artifact;
- transaction/open/commit sequences;
- tool result from call A becomes input to call B;
- two calls mutate the same database, project, file, session, or external service.

Even read-only tools can be unsafe in parallel when they share rate limits, transient sessions, mutable caches, or consistency expectations.

## Regression fixture

Add an application-level scripted chat client that emits at least three tool calls in one assistant turn:

1. append marker `A`;
2. append marker `B`, requiring `A` to exist;
3. append marker `C`, requiring `B` to exist.

Each tool records:

- start sequence;
- completion sequence;
- active invocation count;
- maximum simultaneous invocation count;
- stable call ID.

Acceptance:

- observed result is exactly `A,B,C`;
- maximum simultaneous invocation count is 1;
- each call runs once;
- streaming and non-streaming paths both preserve the policy where both are supported;
- the test fails when concurrency is deliberately enabled in the fixture, proving the probe is meaningful.

Add a negative test for two side-effect calls where the second rejects execution if the first has not committed.

## Approval interaction

A pending approval is a dependency barrier. Never execute later tool calls from the same turn concurrently around it.

Existing approval round-trip tests must prove:

- no governed tool invocation before approval;
- approval replay does not invoke again;
- unknown/cross-session approval does not invoke;
- consecutive approvals preserve binding;
- native stored arguments win over tampered display/persistence data.

## Deferred future design

A future concurrency initiative may introduce a planner with these inputs:

- explicit tool effect classification;
- resource keys;
- read/write sets;
- dependency edges;
- commutativity declaration;
- approval boundary;
- transaction/compensation model;
- per-provider limits;
- maximum degree of parallelism;
- cancellation semantics;
- deterministic result ordering.

Until that exists, no tool or provider may locally opt into concurrent execution.
