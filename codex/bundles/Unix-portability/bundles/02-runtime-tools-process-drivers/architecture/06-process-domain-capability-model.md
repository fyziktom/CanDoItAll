# Process-domain capability model

## Host capability facts

Examples:

- `host.exec.direct`
- `host.exec.pwsh-script`
- `host.exec.posix-script`
- `host.runtime.dotnet`
- `host.runtime.python`
- `host.runtime.node`
- `host.container.docker`
- `host.mcp.local-stdio`
- `host.desktop.open`
- `host.terminal.interactive`
- `host.elevation.windows-runas`

The exact naming is decided during B06 and must reuse existing capability models where possible.

A capability descriptor states availability and execution port. It does not grant authorization.

## Process strategy contract

A process strategy/driver can declare:

- required capabilities;
- optional/alternative capabilities;
- supported process/template versions;
- expected evidence/receipt types;
- preflight validation;
- recovery behavior for unavailable/failed capability.

`Processes` decides whether to:

- select the strategy;
- choose an alternative;
- block before side effects;
- escalate/recover according to process semantics.

## Invariants

- Canonical execution authority remains the source of workspace/tool/mutation permission.
- Host capabilities cannot widen allowed operations.
- MAF maps generic execution; it does not interpret a process failure.
- Process receipts contain logical paths/capability IDs and bounded host facts.
- `ProcessDriverLayer.Platform` is a process strategy layer, not a generic OS utility layer.
