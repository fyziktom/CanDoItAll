# Original Request

User request from 2026-04-26:

```text
I checked the code and I really do not like what we have in ProcessRunAutomationDispatchService.ExecutionPrompt.cs.
It is not correct. There are too many instructions related to just development of dotnet app especially calculator. The main platform must be flexible. It is not just for coding. Those more specific instructions must be part of the instructions of specific agents, not part of the main prompt.
You must improve our default agents. The good will be to add specialized agent for architecture, writing dotnet apps, qa review agent, etc. that are specialized for dotnet by instructions and skills.
Similar way add architect, developer and qa review for JS.
Similar way add business strategists, financial strategist, marketing specialist agents. They can represent group of agents for no coding related tasks. For example preparing some business plan of some project. This will also need some default processes that we can use for testing tasks like this.
they all needs different instructions different approach how to create folder structure for those projects, running them, etc. Thats why we cannot have so tight instruction in basic prompt in process run.
We will use processes also for no coding tasks such as processing emails, creating business plans, analysis of excel sheets, etc. Thats why the basic process agent instructions must be flexible to match different usecases.

This task will require real validations. You will have to run those processes. Some part you can test with agents mockups, but some part you must validate with real agents on real scenarios. Start with more atomic testing. Giving agent some prepared input and analyze if they can return expected artifacts and other informations in shape we need. when those more atomic parts are working correctly you can test them with handoffs where they giving each other proper informations and process flows as it is described.
For testing use PostgreSQL. SQLite is too slow.

Research this problem and use [$candoitall-bundle-workflow](C:\Users\lucys\.codex\skills\candoitall-bundle-workflow\SKILL.md) to solve this as bundle.
```
