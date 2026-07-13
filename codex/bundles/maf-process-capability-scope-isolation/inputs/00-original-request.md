# Original Request

Source: raw user request on 2026-07-07.

```text
Use candoitall-bundle-workflow to solve this:

I found some domain leaks in Maf wrapper. For example (but not only one) is file src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs.
You can see there functions like NormalizeImageSetAnalysisPrompt that creates prompt related to software development. But in different process image might be analyzed because of different reason than just UI design proposal. If we want to have it as specialized development tool for agent it is possible, but then it should have own project with "dev related agent tools" and do not mix it into common workspace tools.
We must avoid those domain leaks, but same time have proper way how those specific instructions can be added trough the processes.
It is related also to tools usage, because from side of instructions it is not just about delivery of specific additional prompts parts for agent, but also about limiting usage of some tools, skills or mcps.
Analyze in this run also the processes and this system of cooperation with Maf wrapper. We did lots of refactoring around tools to make their architectrue more flexible (still not done totally, but better than before).
The main point is to have good channel how process can limit tools,skills,mcps or add some specific instruction (maybe as forced tool that must be used and that define those additional informations, etc).
this is especially important for the case that if we have agent that normally has skill for development and project management but we want it in that process step to do just some management work, we do not want to change agent main settings to remove that skill, we just need tu supress it so it does not even go to agent context during its work.
Our processes needs better refactoring too. We did it before our new csharp-modular-refactoring and other Csharp skills (you must use those skills now). We must do refactoring in phases. Preparation on side of MAF first (isolation of domain leaks, preparation/repair/improvement of supress mechanism) and then refactoring of processes and connection of all improvements/changes and then test together.
it is very complex. Prepare bundle only now.
```

## Preparation Constraint

This run prepares the bundle only. Production code changes, migrations, commits, and PR work are out of scope.
