# Original Request

Captured verbatim from the user on 2026-07-12:

> Main goal:
> improve our workflows architecture and implementation and test coverage and add missing executors nodes.
>
> Architect notes:
> - our workflows are not so bad in architecture, but still it is not ideal. use our CSharp skills ([$csharp-architecture-governor](C:\\Users\\lucys\\.codex\\skills\\csharp-architecture-governor\\SKILL.md), [$csharp-modular-refactoring](C:\\Users\\lucys\\.codex\\skills\\csharp-modular-refactoring\\SKILL.md) and others) to analyze and propose improvements for better testabilitiy and flexibility of the architecture.
> - Remember that plugins adds executors too, so it must be improved there too.
> - add missing executor nodes. we have lots of new tools around files or markitdown in c# (check in MAF tools) that must be added as executors too. It is good to find proper way to use one implementation of key function instead of having own implementation in tools and second in workflows executor. Analyze how we do it now and what is best way how to do this. Maybe we already use tools just wrapped with some executor wrapper. but I am not sure (goal is to have one implementation if possible and reasonable and use it in two/more cases).
> - Usuall system how workflows starts is from project structure, by scheduler, agent can call it as tool (or should be) or as "subprocess" in processes.
> - I think we are missing tokens consumption and prices analytics around the workflows. we need it similar as we have for processes. we must know how much did it cost, how much tokens of what model and how long did it took.
> - Analyze UI for workflows and assure it has all new executors available and they have proper settings components-rendereds based on their capability. We must have this done kind of flexbile and well isolated because executors from plugins can add own scheme for executors setting.
> - We need UI for large-screen only. We do not need small and medium screens now. do not waste time on them.
