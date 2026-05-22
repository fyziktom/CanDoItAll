# Original Request

User reported that many unrelated flows fail because Cognitive Memory participates in agent execution:

```text
Execution run failed for OpenAI default: Agent context contributor 'cognitive-memory.context' reported failure: Cognitive Memory context requires a project scope.

CanDoItAll.AgentFramework.Core.AgentContextContributionException: Agent context contributor 'cognitive-memory.context' reported failure: Cognitive Memory context requires a project scope.
   at CanDoItAll.AgentFramework.Maf.MafAgentContextContributionProvider.ContributeAsync(...)
   at CanDoItAll.AgentFramework.Maf.MafAgentContextContributionProvider.ProvideMessagesAsync(...)
   at Microsoft.Agents.AI.ChatClientAgent.PrepareSessionAndMessagesAsync(...)
   at Microsoft.Agents.AI.LoggingAgent.RunCoreStreamingAsync(...)
```

Concrete reproduction context from the user:

```text
It happened in chat with Portfolio Architect agent in TetrisGame project structure canvas.
It is in candoitall development postgresql db.
```

Requested outcome:

```text
I need to record demo of all functions.
I need you to add some global setting, that cognitive memory is not used and you must add it into all places where it is kind of connected to other parts.
It must be possible to turn it on/off during runtime.
Something like "Using of Cognitive Memory" Enabled/Disabled.
If it is disabled it must skip those steps.
Like in this case of agent chat it will not call memory so those exceptions cannot happen.
After you solve it setup for me clean development db so I can test whole flow again by myself.
```
