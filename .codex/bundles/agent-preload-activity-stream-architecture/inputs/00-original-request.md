# Original Request

## Main goal

Improve loading info for agents and improve UI feedback-events about what agent-and its preload is actually doing.

## Architect notes

### Actual problems

When I open some agent (it is same for floating agents and for example manger of process in process page-manager tab), and I start chat with it, it takes long time before it even start initializing tools and other things. I know that it should capture data in that phase, but on UI it looks like kind of freeze. There is rotation spinner, but no info about what is it actually doing. We must have improved this information events because we will need them later on API in form of SSE too (not now, we must first tune it).

Also the loading is kind of strange. We should have some prepared instnaces of agents in DI, same time we usually have some preloaded data in modules that agents can use almost instantly especially if the agent as itself is before main init. I see it in chat window, that first it is "frozen"(probably loading data) and then it start streaming in chat things like "initin tools, etc" and then it start using llm and doing actual work. So for example in case of work over project structure it should just grab immediate data from opened page-tab (for example project structure tree and selected items list), that is immediate and when agent starts working it might decide to load also additional deeper info.

Similar in processes runs. when I am already in manager tab and select some process run, it means that snapshot of that process run must be loaded already, so loading should be fast and when prompt contains some deeper request over data, then it can load during work what is necessary. From snapshot it can already answer lots of things without some deeper db search.

All those behaviours shows we have some architecture mess around that parts. Sure we need to provide good info for agent work, but we must prefer the runtime informations if possible first or use also some parallel loading during agent initializing. We can await all so it will not start true work before it is loaded and data are ready, but it could speed up the loading for user. I guess we do not use power of C# in paralelism now too much. We must be careful with that, but in some situations it can help us a lot. Still Db can be bottleneck in lots of situations, but when we have some data already loaded in module it might speed it up even more. To use that safe we should first focus on read only operations for paralelism and if it can be dangerous use Interlocked library to do proper snapshots to assure that thread is not grabbing it during some update (typically user is in project structure and ask agent to do something and same time user starts writing in project structure new nodes).

### Event system

We must have properly designed and isolated event bus system. It must be sharable across the modules but still offer proper organisation of the pubsubs. I used to think about using the mqtt for it, but in c# we can do it own and more optimized for our needs and thing like mqtt or opcua we can add for projections similar as we will do later projection to SSE API. SSE for outside connection is actually better because it allows better split of api points so not everyone can have for example to access to all events from all agents or processes and it can limit them to ask for just some specific one stream of events. We should avoid using cache now. keys are usually strings and it would be very hard for refactoring and improvements. we might add cache later, but now try to avoid it unless it is absolutelly necessary (like we have in storage driver for browsing files). It is usually better to use snapshots prepared with use of Interlocked and provide them via some DI service. But even those snapshots must have strict policies of updating and providing info about its lifetime, so it will not split source of truth too much. Very dangerous thing is some forgotten notupdated snapshot that will then override some real truth.

We might have some partial event bus already, but it must be reviewed because it will play large role in solving this and might be much easier for UI to grab informations, etc. Analyze this deeply. It is not small task.

### Necessary actions

You must first do deep exploration and understanding of actual implementations around those parts. Modules must have some standardized systems of this, but adoptable for specific module needs.

You must identify our root causes. Then identify risks that might be related to cross threading, spliting source of truth, bottlenecks, antipatterns, etc.

Based on that you must propose architecture improvements and how to properly isolate them and cover them with tests.

You must review the architecture to assure that it will not break our general functionalities over modules.

Then you must define plan and how to safe implement it and validate it.

You must split it into phases, first all backend things and when it works and you measure true improvements then you can add it into UI layer.

Event I mentioned some specific usecases, this is generic thing related to all even some new modules. Think about it during architecture desing.

I think it might help you to solve this as workflow bundle. It will be large one, do not simplify this. It is very important part of our architecture and must be solved with deep focus and best practices.

Assure you will use at least `candoitall-bundle-workflow`, `csharp-architecture-governor` (and other Csharp skills), `analyzing-dotnet-performance`, `optimizing-dotnet-performance`, `optimizing-ef-core-queries`.

When you will be testing agents switch them from Terra to gpt-5.4-mini so it will not spend too much money on api.

When you finish you must also update all docs here and apis docs and skills that have them now in `C:\repositories\CanDoItAll.SharedInfo` where you must update apis info and skills.

Then rebuild and restart our 5032 instance so I can do more detailed testing.

## Applicable engineering instructions

- Work as a pragmatic senior C#/.NET and Blazor architecture peer.
- Prefer the smallest correct change, strongly typed code, explicit error handling, and strict UI/application/domain/infrastructure separation.
- Do not introduce silent fallback mechanisms, magic-string identifiers, trivial abstractions, XML documentation comments, or mobile-specific UI work.
- Existing Radzen components and the repository component library must be used.
- Code uses fully cuddled Egyptian braces and one statement per line.
