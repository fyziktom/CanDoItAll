# Original Request

User role request:

```text
You are senior C# architect.
I need you to use [$candoitall-bundle-workflow](C:\Users\lucys\.codex\skills\candoitall-bundle-workflow\SKILL.md) to prepare bundle with detailed subbundles for solving this. Your role is planning only. do not do implementation.
Main goal:
we need to add AI workflows into application. We will use SDK of Microsoft Agent Framework. We are using it via nuget package, but I cloned also source for references here "C:\repositories\agent-framework". You must use it to understand what MAF offers for creating and running workflows.

Main notes from human architect:
- we are thinking about workflows as kind of possible substitute of ai agents for some work. They are kind of on same level for us. It does not replace our processes. Our processes are above it. It means that when I will run some process I can decide if role will be filled with ai agent or some workflow.
- We will have to split subbundles into phases. First phase will be improvement of our MAF wrapper libraries to have all necessary models, helpers, wrappers for using workflows as they are in MAF. Next phase will be improvement of agents module (we will keep it in same module, but it will have own page in web app). Then another phase is intergration in web app. After each phase we must run architecture reviews to be sure we are building on good base. Most important is to do detailed review after phase1.
- workflows will need own system of settings and testing of it (from view of APIs and UI similar as we have in processes). Workflow will need own canvas editor similar as we have for processes. It will also contains artefacts, possible human in loop, using agents for some workflow step, etc. It is little similar, but not the same as our processes. Most of the time workflow will just call LLM with strict instructions and just grab result and move it as input for another step or some triage call of LLM or some strict logic.
- It will be good to have some system of prepared "LLM Call Component" . It can be as prepared block for building workflows. It will be good to have kind of library of those components. It will contains some details about what provider/model must be used, type of modality, settings of model if applicable, instructions, shape of result, etc.
- analyze how MAF works with runtime of workflows. if it is possible we can use their runtime core if they have it. If they do not have run core for workflows and they must be managed by some handler from above we need that core as part of our wrapper or maybe for cleaner architecture as additional library that is used to run and manage workflows (it must allow clean parallism, observations, etc similar as process core we have).

remember you are only preparing detailed bundle. It is very complex task. you must be very detailed and assure that implemenation agent will do all and do not skip anything, especially architecture reviews and improvements on the fly.
```
