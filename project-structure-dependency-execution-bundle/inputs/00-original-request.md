# Original Request

Use [$candoitall-bundle-workflow](C:\\Users\\dell\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to add new feature.

Main goal:
- we must be able to tell, that some node can be executed only after something else is done.
- each node can be dependend on finish of one or multiple nodes and any node can be dependency for one or multiple other nodes
- this will later serve for creating gantt graphs.
- it must be function of all types of nodes including simple notes.

Notes from architect:
- in UI it will be good to add section in canvas top toolbar with few simple tools like: select (icon "arrow_selector_tool"), dependency (icon "start"), delete (icon "delete"). When I click on dependency button mouse cursor will change in canvas to something different and I will see the curve comming from node I was selected on and I can connect it to some node (it means selected node will be dependend on finish of that I will connect to). it must be still possilbe to use mouse left click for move. It means the accepting of the connection will happen when I click to some second block (so left click will be available for move around unless I click on some node).
- Delete must work also for those connections. If I click to delete tool in toolbar it will use standard cursor, but whatever I mouseover will highlight (with some border and visual efect) and if I left click on it it will delete it. If i try to delete node with multiple nodes it must ask me if I am sure.
- connection curve must have arrow that shows direction of dependency.
- connection must be kept even if I am moving node around
- there might be usefull to have some good driver for providing informations about dependencies. we will need it for creating gantt graphs from it and also as information channel for project structure mcp server (for example ai agent can ask if it can do some task node...if all dependencies are done already...or it should wait or do first some different task).
- Part of that driver can be subdriver (we can have different versions) to convert dependencies into gantt graph. Best format will be mermaid. If some node does not have information about time lenght (most of them will be like that) it should use some default like 1 hour lenght.
- It will be probably good to have in each node some prepared property for timespan (probably in seconds to keep it small in memory, but readable...means better than ticks or ms, etc).
- you must test all with playwriht mcp and screenshots validations!

Important:
use new sqlite db for those tests, do not use our legacy db. In new db you can do much larger structures from nodes. I think it might be nice test to convert the prepared bundle first into mindmaps (in that new db as new project+subprojects via candoitall project structure mcp server that you have) and use it as test mockup data. Analyze properly what type of node to use for what step in bundle (some things are notes, some things are tasks, phases, subprojects, etc). You can same time test to provide progress info into nodes (about what you already done during bundle execution, validation phases).
