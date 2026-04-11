# Original Request

## Initial Request

```text
branching is not correctly added to canvas.
when I add branch it must create new node connected to one where I clicked right click to add branch. it means branch is represented by own node. That node allows curve connection from matched output (if I have 5 possile output, there will be one curve from each plus one more for default and one more for error). those curves can be connected to another node/part of the process. It means, that in canvas I can connect what will happen if some process step will create some output and branch decesion will select some swtich option. For decesion make there must be possibility to add input curve from some role definition node.

Our actual node objects are created to have one or more connection from side, but centered from one point. Maybe for this purpose it is good to add another advanced type of nodes that allows multiple inputs outputs. Do not remove or change old ones. Add new as optional element we can use in the canvases when we need. Look at the example how they look on screenshot. Thats what we need for this.
This is large task, so you must use [$candoitall-bundle-workflow](C:\\Users\\lucys\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to prepare detailed bundle with subbubndles for all steps. You must do real validations with playwright mcp and screenshots. Especially canvas related things you must validate from screenshot analysis. You must add proper examples of processes with branching. Do it on scenarios around software development. There are great processes like code reviews, when code must go back to repairs, qa, and in loops like that where different roles must approve it until it is ready for merge and process is done.
You must record all troubles you will face according to our architecture. maybe we are missing something imortant to be able to do processes like this. It is good to start with defining those proceses first and then check what we are missing. if something missing it can be in first subbundles to add it first before changes happens.
```

## Follow-up Request 1

```text
great, just few things you must tune. 
1) the modals that shows when I click on something in canvas must have higher z-index. otherwise when canvas is maximized I cannot see them. 
2) add delete tool same way as we have in project structure canvas.
3) when I click with right click on circle that red arrow is aiming on screenshot on any of those process/roles, etc nodes in processes canvas, it will start drawing curve and I can do connection to some different node input. When I click on that circle on different block it will assign properly connection based on what node and its input I clicked on to finish connection.
4) test all improvements
```

## Follow-up Request 2

```text
It does not work properly. At first lets change starting doing connection between process blocks with left click on that small circle. You can see the steps on the screenshot. first left click on that small circle, then it is drawing line (it is working) and then I left click on some specific circle on some another node and that confirms the connection. Circles on nodes are not correctly positioned now. You can see it on the screenshot. They must be exactly on those badges that explains what input/output is it. There is also one missing circle over badge with "Review Lead" on process-branch-router node. 
btw, each node can have many to many connection. For example some router, or decesion can have one input point for "artefacts" or "inputs" and for example multiple other processes blocks will have connected output to that point. When they all deliver output, then process use all of those inputs for some job.
Assure that you have proper scenarios of processes for this and analyze from screenshots all the details I described that it must have. 
Then assure about proper work with db. I had some feeling, that it might not be correct. I moved some node with role on canvas, then I doublecliked on something, and the block was back on its original position. We must avare of canonical troubles, but same time we must assure that actions like this are stored properly.
those are lots of tasks, so improve bundle with subbundles for solving all those points first. Then you execute then and do detailed validation and testing. 
```

- Inline screenshot attachments were included in the same Codex thread and are preserved by reference in `inputs/03-inline-screenshot-reference.md`.
