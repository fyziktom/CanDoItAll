# Original Request

Source: direct user request on 2026-05-04.

```text
when I open processes page in https://localhost:7271/ I see that there is "55 active runs" but most of them are blocked or failed. It should show just running processes. Add also other badges with info about how many failing we have or blocked. Those informations must be provided by some generic service about state of the running processes and their details. Our UI will have to be more flexible and display more what is happenning now, so service like that will help both actual and future UI. We can also use it later as tool for "Manager agent" that will observe processes in generic way, etc. I think it might be related also to what I am writing bellow about lazy loading. This service will help us to prevent multiple loading of same thing, because it can be for UI kind of controlled cache. But be carefull it must not split the source of truth.

Also we must have in UI option to stop blocked processes in list in selected process "Runs" tab.
I think those blocked or failed processes can create some slow loading of processes page we have now. Analyze how we load the data when we open processes page. We must have some kind of lazy loading. For example if I do not open Run tab of some processes it should not have preloaded data. Only if they are necessary we must load them. Otherwise it will be too slow. There is lots of informations around running processes.
analyze it and improve it. Use [$candoitall-bundle-workflow](C:\\Users\\lucys\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to solve this.
```
