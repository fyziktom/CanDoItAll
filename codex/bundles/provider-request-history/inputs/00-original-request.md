# Original Request

Received 2026-08-28. The request is preserved verbatim, including HTML entities.

```text
You are senior C# architect.

I found thing we must improve in our sharing providers.&#x20;
When I looked at shared instance ([http://localhost:5210/agents](http://localhost:5210/agents)) it shows that shared provider was used. But it shows as unpriced. We also have no way how to get to logs what client/apikey used what. We must have at least basic log in providers (some of those things like matching with exact users based on IDM will solve later EGCP).
I think we need two of those logs/history search. One in providers as another tab next to tab Sharing for each provider. It will not load data immediatelly. Only upon request and selected range/filters (similar as we have in "Manager Summary" tab in project structure).&#x20;
Then we must have one more tab in agents page dedicated to history search over all providers. Same it will load only upon the request.&#x20;
Some data we are already storing (like agents chat history, simple chats history, workflows, etc) so we do not want to double all those records (like one llm response in history of agent and second in history of llm provider requests), so all already tracked records will be matched by provider id (and model) and untracked must be stored (based on some general settings how much history we should keep, etc.). We can have some settings like store just light logs (shorten version of log about some provider request) vs store detailed logs (store also prompt and response). But this might be tricky for longer conversations. We can have becaue of that lots of duplicated data if prompt is always assembled like all previous messages.
This is not simple task. We already have some prepared parts for it and just some of them are missing.
You must first do detailed analysis and design of architecture. You must avoid some wrong dependencies, or too large files, or other antipatterns in architecture.&#x20;
Use [$csharp-architecture-governor](C:\Users\lucys\\.codex\skills\csharp-architecture-governor\SKILL.md) and [$analyzing-dotnet-performance](C:\Users\lucys\\.codex\skills\analyzing-dotnet-performance\SKILL.md) , [$optimizing-dotnet-performance](C:\Users\lucys\\.codex\skills\optimizing-dotnet-performance\SKILL.md) and other C# related skills to assure that architecture is correct.&#x20;
Prepare the bundle only now.
```

The accompanying workspace instructions require strongly typed C#, small correct changes,
explicit errors, separate UI/application/domain/infrastructure responsibilities, existing
Razor component reuse, no unsolicited XML comments, and English comments only when needed.
Implementation must use fully cuddled Egyptian braces and one statement per line.
