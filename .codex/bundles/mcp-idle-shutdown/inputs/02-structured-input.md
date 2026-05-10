# Structured Input

## Notes

| Id | Raw note | Normalized requirement | Owning subbundle | Proof expectation |
| --- | --- | --- | --- | --- |
| N001 | "during your work I see like 10-20 of instances running same time" | MCP stdio hosts must not run forever after tool activity stops. | `01-shared-idle-shutdown` | Shared idle lifetime service test plus MCP project build. |
| N002 | "for components mcp it is really not necessary because it is more documentation style mcp" | Components MCP should have a shorter default inactivity timeout than SSH Ops. | `01-shared-idle-shutdown` | Options/default tests or source inspection plus Components tool activity test. |
| N003 | "in both cases they should shut down after some time of innactivity" | Both Components and SSH Ops must wire the idle shutdown policy through configuration and mark activity on tool invocation. | `01-shared-idle-shutdown` | Targeted tests pass for shared lifetime, Components tool wrapper, and SSH Ops wiring/build. |
