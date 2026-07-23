# Original Request

The following user input is preserved literally.

```text
Main goal:
Improve our dashboard page.

Architect notes:
- right now the dashboard page does not contains usefull information. We will keep just quick actions but with different actions than now.
- we should display this:
  - quick actions: Project, Agents, Live Processes, Scheduler. Make them more compact. Square card with centered icon and title under it.
  - last 5 updated projects
  - active or last 5 runned workflows
  - active or last 5 runned processes (it can be as one card with tabs together with workflows)
  - Total use of tokens and price (what we have on agents tabs

- snapshot of those data should not be loaded everytime user go to dashboard tab. It would take too much resources. we should do snapshot like one per 5 minutes and also allow forced refresh by user. There must be info about how much time remains to automatic load (as tiny text next to refresh button).

use candoitall-bundle-workflow to solve this as bundle and use our Csharp skills like csharp-architecture-governor to assure that architecture is correctly designed and use analyzing-dotnet-performance and optimizing-dotnet-performance to assure that it will not create any bottlenecks or overloading of db or other troubles that could cause performance troubles.
```
