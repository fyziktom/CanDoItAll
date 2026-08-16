# Architecture Analysis

- Snapshot build: `code-analytics_b4cca1586a0c4d4686fb1dd91b17ad83`.
- Snapshot: `snap-20260816214112-d26d371e`.
- Dependency query: `code-analytics_798b314d07f64eec95cbba57d3896ef0`.
- Fresh/cache: fresh (`fromCache=false`).
- Blocking errors: none.
- Scope: six named Components, AgentFramework, LlmChats, persistence, and Web projects.
- Counts: 971 types, 8,592 members, 87 service registrations, 344 findings, 18 open questions, 20 non-blocking diagnostics.

The project dependency direction matches the prepared architecture. `Conversations.Components` has no project references. No reference changed in SB05. The structural suffix `d26d371e` matches the earlier baseline exactly. The same known AgentFramework module and type cycles remain; no new cycle was introduced by the accumulated work.
