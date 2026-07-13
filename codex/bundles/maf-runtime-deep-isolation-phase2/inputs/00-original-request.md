# Original Request

User follow-up on 2026-07-06:

> this was just very small part of the isolation. Why are those builders and other parts inside partial class of MafAgentRuntime? It needs correct isolation. this is not fully done. 
> There is plenty of the classes that are kind of hidden under that MafAgentRuntime. It is very hard to understand the structure of the code becaue of that.
> analyze this and propose architecture improvements how to make it more maintainable and isolated for better unit testing. You must avoid to add everything under mafagentruntime. you must find better approach and prepare all necessary steps to repair actual implementation. this is lots of work so first just prepare new bundle for this next phase. do not implement it yet.

Context from the previous implementation phase:

- Several seams were extracted, but `MafAgentRuntime` still owns many private nested builders and DTOs.
- The user explicitly rejected treating the previous pass as complete.
- The next bundle must focus on generic MAF runtime architecture, not agent-specific financial/document behavior.
