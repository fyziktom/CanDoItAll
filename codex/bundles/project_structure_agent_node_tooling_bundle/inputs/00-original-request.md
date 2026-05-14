# Original Request

Use [$candoitall-bundle-workflow](C:\\Users\\dell\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to solve this: 
1) when I open project structure, the title of page must be "PS - NAME OF PROJECT". When project name is too long make it shorter with substring and "...".
2) I tested agent to add work task nodes into project structure. It was not able to do it. and if I more force it it added as different node thant work task node.
Analyze if agents has in project structure default tool all available nodes with proper info.
agents must be able to do even more complex tasks over nodes. This is very imporant.
For example if I select some nodes group and then go to chat with agent in project structure I must be able to say it "take selected nodes and move them to own new subproject named XYZ" and it will see what nodes are selceted, then it creates new subproject, connect subproject into opened project and then cut selected nodes and paste them into subproject. It must also assure that all nodes has proper connection to some parent node.
For project structure is important also dependency connections. Especially for tasks nodes, but they can be applied for any nodes. It Is very important that agents will understand how to do it and when to do it (for example adding multiple tasks nodes it must consider their dependecies too). Based on dependecies we are creating gantt graph then.

Important Notes from architect:
Consider what will be best approach for this. Project structure tools/nodes are more and more complex. we do not want to have everything just in code. But only skill is also not the solution. it must be some combination of agents skills for project structure and also some prepared tools. For example that cut+paste scenarion could be created as specific tool.
Identify some generic situations like this where it makes sense to help agent to do it in one tool call. It is best to create xlsx with all those generic scenarios.
Just few others I got in mind and they might cost agents multiple not necessary steps:
- creating node with some files assets
- moving some multiple nodes or all nodes under selected node into different position ("prompt like...move seleceted node with all under it more to top part of project structure with enough spacing").
- change of selected node/nodes type to different one.

I am sure you will find another similar userstories
