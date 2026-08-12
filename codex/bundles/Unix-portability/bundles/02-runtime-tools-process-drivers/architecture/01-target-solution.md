# Runtime target solution

## Execution pipeline

```mermaid
flowchart LR
    Intent["Runtime node / MCP / tool / plugin intent"] --> Compile["Owner-specific typed plan compiler"]
    Compile --> Authority["Authority + approval + capability validation"]
    Authority --> Resolve["Executable + environment + path resolution"]
    Resolve --> Host["Authoritative process host"]
    Host --> Registry["Owned process registry / lifecycle"]
    Host --> Receipt["Bounded redacted receipt"]
    Registry --> Manager["Manager supervision / recovery adapters"]
    Receipt --> Processes["Processes evidence / semantic interpretation"]
```

## Owner-specific compilers

- Workbench compiles runtime-node metadata.
- MCP compiles local stdio descriptors.
- External tools compile capability descriptors.
- Plugins compile approved recipe arguments.
- Processes selects strategies and interprets outcomes.

They share execution primitives but do not share domain semantics.

## Authoritative execution primitive

Default direction:

- preserve `LocalWorkspaceProcessHost` contracts;
- extract lower-level reusable pieces only when dependency analysis requires it;
- inject the same host/registry into workspace runtime, external tools, Docker/plugin tools, and other local execution surfaces;
- keep boundary/isolation truth explicit: policy-only execution is not an OS sandbox.

## Optional presentation/native adapters

- terminal presentation (Workbench);
- Windows elevation;
- Windows WMI recovery discovery;
- Linux proc recovery discovery;
- macOS recovery discovery;
- native process-group/Job Object adapter only when characterization requires it;
- desktop/FileTools launch.

These do not become mandatory for headless direct execution.

## Processes integration

Process strategies declare required stable capability IDs. Processes decides:

- whether a strategy is eligible;
- whether an alternate strategy exists;
- how missing capability affects the process;
- how receipts/evidence/recovery/escalation are interpreted.

Host adapters report facts and execute authorized plans; they do not decide process meaning.

## Authoritative boundary map

| Responsibility | Authoritative owner | Consumers | Forbidden drift |
|---|---|---|---|
| Typed local execution facts | AgentFramework execution primitive | Workspace commands, MCP adapters, tools, plugins, Manager adapters | Domain eligibility or process meaning in the host |
| Executable and environment policy | Capability-owning compiler plus shared host-correct primitives | All local launch surfaces | Universal case-insensitive or Windows-suffix policy |
| Runtime-node intent and terminal presentation | Workbench | UI and project-structure tools | Shell text used as a generic process protocol |
| Supervision and host-process recovery | Manager | Watch and tuning operations | Name-only ownership or foreign-process termination |
| MCP stdio session semantics | MCP runtime | MCP capabilities | A second generic process policy stack |
| Plugin host recipes | Plugin capability owner | Approved plugins | Plugin-local process-host lifetime and generic OS policy |
| Process eligibility, evidence, recovery meaning, and escalation | Processes | Process strategies and projections | MAF or Infrastructure deciding domain outcomes |
| Native secret helper execution | Security | Vault providers | Migration into the generic runtime host without a security gate |
| Desktop file launch | FileTools integration | Workbench operator actions | Support claims without tested external profiles |

MAF may report execution facts and enforce generic invocation policy. It must not interpret those facts as process-domain success, recovery eligibility, escalation, or evidence sufficiency. Infrastructure remains an inward technical boundary and does not acquire process semantics.
