# Hard Constraints

- Do not create `CanDoItAll.Processes.Core`.
- Do not create production process driver APIs, registries, or driver packs.
- Do not move EF entities, DbContext access, UI/Razor files, MAF runtime composition, or Tooling contracts.
- Do not change public process tool names.
- Do not weaken approval/access policy.
- Do not hide `SaveAgentAsync`, recovery journal mutation, transition execution, workflow calls, subprocess calls, or execution-client calls inside a pure-looking factory.
- Do not test or optimize small/medium/mobile viewports.
- Browser validation is N/A unless UI files change unexpectedly.
