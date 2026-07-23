# Original Request

I found bug in the gantt in projects.

I tried to open task that has assigned person and agent. it should be possible to open it. 
"
Task details unavailable
The task has multiple direct assignees. Resolve the assignment conflict before editing it from the Gantt chart."

then I found that our setup of price of some resource from CRM is not reflected in task creating. If task did not happen and it is created or updated it should take price from the crm. In case of agents, processes or workflows we must use some strategy (strategy pattern) that will run estimation mechanism to get correct estimate of price. 

I checked that in case of our workforce we do not have setup price in crm but in task they have it. it means it is not connected now. 

Imporant note:
Our actual project structure page is starting to be harder and harder to manage. There is one large class split to partial classes only instead of proper isolation into helpers, abstractions, drivers, strategies, etc.
Solve this as bundle with `candoitall-bundle-workflow` and use `csharp-architecture-governor` and `csharp-modular-refactoring` to improve at least part of the architecture around project structure. I expect that it can help us alot to have maintanable solution and also do proper unit test coverages for problems like I described above. 
You must not remove any existing functionalities. There is lots of things that project structure must handle, so in case of changes you must assure about proper testing/validation that functionalities are working as before, just with more quality architecture.
