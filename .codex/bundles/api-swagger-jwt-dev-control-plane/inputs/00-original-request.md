# Original Request

- Source: User request in Codex thread on 2026-05-05

```text
Main goal:
we must have add api with swagger and optional jwt authorization of that api to access projects, processes and agents.

Notes:
- we already have logic for getting info or changing projects and processes. same things happens via UI or mcp servers or agents tools. you must try to unify those logics when they are same. it is good to create some service or helpers to prevent doubling of same logic. This is very important. Our solution is already very large so we must prevent doubling of same code whenever is possible (and safe for sure. Still need to understand limits due to paralelism/threads blocks).
- we are doing it to have better access to data and control over the app during the development. when we are doing it via mcps it is not good when we are starting app under different ports, etc. We must have detailed access via API. During bundle preparation you must map all things that might be helpful during development around projects and processes.
Small example: it is not enough to just get data about run of some process, we need also possibility to chat with process manager about that run, be able to edit processes, etc.
When we run the processes via project structure node we must have those things on api too including HR matching of the resources, so you can control whole flow of creating and executing project.
Think it through. It is good to write down all userstories into xlsx where you can then check it if all is covered with proper implementation and review and refactor.
- It also helps when api will alow to do proper filtering. When you work with processes testing it is easy to overload your context. Proper filtering can help to better focus the work (for example: you could ask for artefact from specific process step and not just all artefacts from process).
- jwt will be optional in appsetings.json it can start without it (default option) or with it. It must have section in Settings in UI (if active you can create tokens there, etc.)
- it helps if each few subbundles you will do analysis/review of the architecture if it goes good direction. If there are things to improve you must add another onfly-subbundles first to repair it before contiunig with another subbundles.
```
