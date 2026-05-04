# Original Request

Use `C:\Users\lucys\.codex\skills\candoitall-bundle-workflow\SKILL.md` to solve this:

Main goal:
in processes we must have possibility to use another process as process step (PrS).

Notes:

- process that is as PrS must be observed so main process can report what is happening.
- there might be lots of processes and subprocesses running. it is lots of data and threads. think deeply about the paralelism, optimisations, prevent of spliting source of truths, etc. it is not simple task.
- We must have some ai managers over the running processes. In default there will be some basic, but it will be possible to override it per process with own selection of the specific ai agent for role of manager. if override is applied the HR matching during run of process will automatically take that one. For person it is too much informations, so we need to have chance to get reports like from standard human managers about what is happening, what are main blockers and also possibility to instruct manager what to do to unblock it.
- subprocesses will be necessary. our main sw development process is starting to be too large and still we have troubles with some steps. it is also because we need to split them into subprocesses (for example main implementation must be split into steps like (for example for .net) "create empty solution with specific names and subprojects from blazor SSR template", "add unit test project in solution with xunit", "....".... etc. Than we will be more successful with running more complex processes. You must add those processes in our default templates same as proper agents or improving of their skills.
- it must be possible to add/change subprocess in processes canvas and ui. in canva it must be as option in right click menu. when I double click on some subprocess it must open it as new browser tab.
- subprocesses in canvas must have own specific visual style.

Mandatory steps:

- you must do detailed design of architecture. Do revalidation each few subbundles to be sure you are going good direction. if not, you must do refactoring of the architecture to improve it and then continue.
- analyze also `C:\repositories\agent-framework`. we use now 1.3 that has lots of good features for A2A and handoffs, workflows, etc. It can help us.
- you must do proper validation and real testing. You must validate it on random realworld small cases. Like we did with those simple apps you can see in projects in main postgresql db.
- go atomically. Assure that subprocess (like .net development with small steps) works fine, than you can test it as part of some higher process.
