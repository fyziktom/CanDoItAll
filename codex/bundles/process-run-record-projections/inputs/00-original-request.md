# Original Request

## Main goal

Improvement of the working with processes records.

## Actual main troubles

- Whenever we work with historic data about processes the loading takes really long time.
- We need it on more and more places so we must improve it.

## Architect notes

It is not just about finding some actual bottleneck. We must do little larger improvements.

Each process is differently difficult and contains also subprocesses. It is a large amount of data that even for one run takes too much time to load for a basic dashboard, recent manager history, and similar uses. Improve it by storing proper snapshots.

When a process ends no matter how (failed, escalation, success), the manager must assemble a detailed summary. It contains basic hard data such as steps, repetitions, actor IDs, elapsed time, tokens, and costs, plus a structured LLM summary aggregating what was done and what caused trouble.

The summary must be connected through IDs, not database relations that require joins. Lists of string IDs may be stored as JSON. Agents are available from a shared DI service in memory. The stored information must support Runs, Graphs, Analytics, future CRM Recruiting use, LiveProcesses, and a project-structure node when the process ends.

Use the analyzing-dotnet-performance and optimizing-dotnet-performance skills to find performance anti-patterns and sequential/deep-loading bottlenecks, then improve them during architecture preparation and refactoring checkpoints.

Expose the information through the Processes APIs. Update the authoritative Processes API skill in `C:\repositories\CanDoItAll.SharedInfo`.

Use the CanDoItAll bundle workflow, C# architecture governor, and C# modular-refactoring skills.
