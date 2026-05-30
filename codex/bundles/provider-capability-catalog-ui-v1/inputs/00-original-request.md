# Original Request

Source: Current chat request on 2026-05-30

```text
great. we have some trouble in providers. the badge on providers tab in agent page shows 4, but in list there is just one.
Use [$candoitall-bundle-workflow](C:\Users\lucys\.codex\skills\candoitall-bundle-workflow\SKILL.md) to analyze it and repair it. and also solve all those points:
- Add default provider for local ollama.
- Change list of providers to treeview. Parent can be based on the by tags. But I think we are missing tags on providers. so if we are missing them, add them and add them also in UI. we have component for TagsEditor.
- Then improve Capabilities tab. Use also treeview we have for agents instead of that list. Then limit cards of the skills/mcp/tools in main detail panel. It must show multiple of them on row (we need only large screen/desktop, do not tune it for smaller screens now). On top of that there must be search and tags and also dropdown of filter for "All, Assigned, Not Assigned" and another for selection of "MCP, Skill, Tool".
On capability page we are also missing possibility of add new MCP server or skill. There must be wizard for setup of new MCP or Skill. This might be more difficult, so analyze it deeply. Use [$imagegen](C:\Users\lucys\.codex\skills\imagegen\SKILL.md) to create visual proposal of UI of each step of that wizard separatelly and then use them to create ASCIII layouts and based on them real implementations. We have step component for generic wizard. Same as we have component for file upload. You must use our component, so proposed UI is mainly about correct layout compositions. You must assure visually that result is close enough to proposals. Assure that all components uses space effective way, they do not overflow or layover each other and those standard troubles.
- each skill on mcp server or tool on capability tab must have some dialog with details where I can see all the info (and edit of tags, name, etc).
In case of MCP servers it must allow to edit the parameters (arguments, path, etc).
in case of default tools it can limit possibilty to edit things, but at least tags should be possible to add (we can use it later when you chat with agent and you need to search specific skills group and add them all to agent chat, for example you want to use all "economy" related skills so you can just add something like /skills-tag:economy and it could add them all for that specific prompt, but do not do it now, just example why we need it)
```
