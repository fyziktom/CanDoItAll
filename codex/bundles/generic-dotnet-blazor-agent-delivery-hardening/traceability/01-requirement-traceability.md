# Requirement Traceability

| Requirement | Source | Owning Subbundle | Planned Proof |
| --- | --- | --- | --- |
| R1 | User asked for generic build/run/test .NET app readiness | 02-dotnet-run-tooling | Unit/integration tests and seeded capability assertions |
| R2 | User asked to improve default agents, instructions, skills, tools | 03-generic-agent-and-blazor-specialist-seeds | Seed integration tests and instruction source scan |
| R3 | User requested specialized Blazor app-building agent | 03-generic-agent-and-blazor-specialist-seeds | Seed catalog test verifies agent, skills, tools, and component guidance |
| R4 | User rejected calculator-specific hardcoding | 01-agent-skill-tool-inventory, 03-generic-agent-and-blazor-specialist-seeds | Source scan for active seeded instructions and skills |
| R5 | User requested BaseLib/components-first Blazor guidance | 03-generic-agent-and-blazor-specialist-seeds | Instruction assertions and live Blazor process behavior |
| R6 | Existing running app must receive updates | 03-generic-agent-and-blazor-specialist-seeds | Managed seed version bump and refresh tests |
| R7 | User required two random-topic web-flow validations under `C:\programovani\dotnet` | 04-live-web-flow-validation | Process-run records, generated paths, build/test/run/browser evidence |
