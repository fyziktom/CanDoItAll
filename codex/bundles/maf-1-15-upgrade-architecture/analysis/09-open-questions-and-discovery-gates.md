# Open Questions and Discovery Gates

Every question below must be resolved in SB01 or explicitly assigned to a later subbundle.

| ID | Question | Why it matters | Resolution method |
|---|---|---|---|
| Q01 | Does any provider factory set `UseProvidedChatClientAsIs = true` or supply a pre-decorated `FunctionInvokingChatClient`? | Determines whether 1.15 default approval middleware is present | Inspect every `IMafProviderAgentFactory` implementation and runtime service registration |
| Q02 | Does any code set the old `EnableNonApprovalRequiredFunctionBypassing` option? | Compile break and behavior baseline | Full branch grep |
| Q03 | Is `MafAgentResponseSnapshotter` sorting, grouping, cloning, or assigning IDs? | Could undo MessageMerger fixes | Locate class and characterize each transform |
| Q04 | Does the provider streaming runner invoke `AIAgent.RunStreamingAsync` with the same restored session? | Binding depends on active `AgentRunContext.Session` | Inspect runner and add middleware-presence test |
| Q05 | Does the attachment scrubber preserve arbitrary state-bag entries? | New approval binding state must survive | Serialize/scrub/deserialize fixture |
| Q06 | Are pending approval records integrity-protected and transactionally consumed? | Legacy bridge and replay security | Inspect persistence model/store and concurrent continuation |
| Q07 | Can more than one pending approval exist despite `AllowMultipleToolCalls = false`? | Current boolean applies to all | Provider/MCP/mixed response tests |
| Q08 | Are all approval request IDs stable and non-null for function and MCP calls? | Random fallback must be removed | Capture real/fake provider fixtures |
| Q09 | What exact workflow output event identifies a terminal handoff result through public APIs? | Needed for streaming authoritative projection | Inspect MAF 1.15 APIs and raw events in fixture |
| Q10 | Can handoff depth be enforced at the transition/tool boundary without rebuilding responses? | Preferred wrapper simplification | Inspect handoff builder/tool middleware extension points |
| Q11 | Does the custom checkpoint bridge persist native MAF checkpoints/external requests? | Determines relevance of assembly identity fix | Trace bridge/storage and capture fixture |
| Q12 | Are AG-UI, declarative workflows, Harness, compaction, FileMemory, ToolApprovalAgent, LocalCodeAct, or Cosmos history active? | Hidden compile/behavior impact | Full branch grep and package graph |
| Q13 | Does any A2A endpoint expose approvals, sessions, or streaming updates? | Defines smoke-test depth | Trace endpoint mapping and agent card |
| Q14 | Which test projects own MAF runtime, workflow, hosting, and integration tests? | Correct placement and CI execution | Solution/test inventory |
| Q15 | Can a 1.15 serialized session be read by 1.13 for rollback? | Rollback design | Bidirectional fixture test |
| Q16 | Is MEAI 10.8 `ToAgentResponse()` behavior compatible with the MAF 1.15 workflow assumptions built against 10.6? | Actual resolved merge semantics | Targeted update sequence tests and package graph |
| Q17 | Are current `MAAI001`/`MAAIW001` suppressions hiding newly relevant warnings? | API stability and future upgrade risk | Build targeted projects without suppressions |
| Q18 | Are required finalizer failures correlated with workflow output projection? | Potential simplification | Baseline and post-fix telemetry |
