# Original Request

```text
branching is not correctly added to canvas.
when I add branch it must create new node connected to one where I clicked right click to add branch. it means branch is represented by own node. That node allows curve connection from matched output (if I have 5 possile output, there will be one curve from each plus one more for default and one more for error). those curves can be connected to another node/part of the process. It means, that in canvas I can connect what will happen if some process step will create some output and branch decesion will select some swtich option. For decesion make there must be possibility to add input curve from some role definition node.

Our actual node objects are created to have one or more connection from side, but centered from one point. Maybe for this purpose it is good to add another advanced type of nodes that allows multiple inputs outputs. Do not remove or change old ones. Add new as optional element we can use in the canvases when we need. Look at the example how they look on screenshot. Thats what we need for this.
This is large task, so you must use [$candoitall-bundle-workflow](C:\\Users\\lucys\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to prepare detailed bundle with subbubndles for all steps. You must do real validations with playwright mcp and screenshots. Especially canvas related things you must validate from screenshot analysis. You must add proper examples of processes with branching. Do it on scenarios around software development. There are great processes like code reviews, when code must go back to repairs, qa, and in loops like that where different roles must approve it until it is ready for merge and process is done.
You must record all troubles you will face according to our architecture. maybe we are missing something imortant to be able to do processes like this. It is good to start with defining those proceses first and then check what we are missing. if something missing it can be in first subbundles to add it first before changes happens.
```

- Inline screenshot attachment was included in the same Codex thread and is preserved by reference in `inputs/03-inline-screenshot-reference.md`.
