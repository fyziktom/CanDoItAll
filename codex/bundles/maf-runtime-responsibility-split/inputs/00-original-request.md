# Original Request

```text
Use [$candoitall-bundle-workflow](C:\\Users\\lucys\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) 
to solve this:

MAF:
- MafAgentRuntime.cs is too large. I think we should split it based on responsibilities and isolate helpers. For example finalizers could be as drivers or some strategies. They are mostly static, so at least as helpers isolation. There are also functions like ComputeStableHash, that should be in some general helpers of whole project. It can be usefull in multiple places. Similar FormatArgumentValue but in that case it might be more some MAF helper and not whole sln helper.
- MafAgentRuntime has split into partial classes, but it still means that it is mixing lots of responsibilities. For example MafAgentRuntime.ModelParameters should be as kind of builder and not mixed in this class as partial class. Same session and context manifest. It should be SessionBuilder and ContextManifestBuilder. 

it is larger refactoring, so first prepare bundle only. use xlsx to create detailed checklists and assure that it contains all related parts and ways how to test them after changes including UI testing.
```

## Follow-Up Repair Request

```text
analyze that trouble with provider and repair it.

I also found some trouble with local providers:

local LLM troubles:
- I tested gptoss20b64k and in chat in project structure it does not responded. I was watching the GPU and it not even started loading model in vram. But in health check in provider setup in agents page providers tab it worked ok and did health check (and ollama loaded model). So it is some bug in agent when it start streaming-sending requests. I assured that Financial Manager has setting to local ollama provider.
- I tested i also with gemma4-12b-256k and same result
- I tested same thing in agents chat in agents page and it acts the same. it does not send anything to ollama.
- when I tested to run workflow with simple llm call, it worked with that local ollama well, so trouble is just in agents.

it might be related. so analyze it, improve bundle to solve all this and trully test it. try also with playwright mcp to chat with agents via UI to assure they are working and can use all tools, mcps, etc. do not fake those tests. do them trully and analyze their outputs. if it is not working repair it and test again.
```
