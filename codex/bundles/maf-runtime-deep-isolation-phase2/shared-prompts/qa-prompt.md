# QA Prompt

Use this prompt for subbundle review and final bundle closure.

```text
Review the MAF runtime deep isolation work as a senior C# architecture QA reviewer.

Check:
- Does MafAgentRuntime still hide private nested builders, DTOs, or large helper classes?
- Do new collaborators have clear responsibility names and explicit dependencies?
- Are direct unit tests targeting extracted collaborators instead of constructing the full runtime?
- Did the implementation avoid a new broad manager/service-locator pattern?
- Are workspace/MCP/finalizer/session behaviors proven with positive and negative tests?
- Did architecture guards fail before the fix or at least prove the forbidden source patterns are absent after the fix?
- Are performance/startup metrics captured?
- Are unrelated full-suite baseline failures documented without being hidden?

Reject closure if:
- any `*CapabilityBuilder` remains private under MafAgentRuntime,
- any extracted builder accepts `MafAgentRuntime owner`,
- tests use reflection to reach moved behavior,
- the runtime remains the only construction path for key collaborator behavior,
- the execution report lacks command transcripts or boundary scans.
```
