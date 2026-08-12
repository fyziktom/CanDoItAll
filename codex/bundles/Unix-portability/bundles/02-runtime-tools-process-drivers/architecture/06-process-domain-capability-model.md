# Process-domain capability model

## Host capability facts

The B06 stable identifiers are:

- `host.exec.direct`
- `host.exec.managed-process-adapter`
- `host.exec.pwsh-script`
- `host.exec.posix-script`
- `host.runtime.dotnet`
- `host.runtime.python`
- `host.runtime.node`
- `host.runtime.node-package-manager`
- `host.container.docker`
- `host.mcp.local-stdio`
- `host.desktop.open`
- `host.terminal.interactive`

A capability fact contains only the stable ID, typed availability, typed reason, and execution port. The scoped snapshot adds a bounded opaque profile token; production examples include `windows`, `linux`, `macos`, and `unknown`, while tests and dispatch adapters may use equally bounded synthetic identities. It cannot carry an executable path, socket endpoint, provider message, secret, workspace authority, or approval decision.

Host adapters project existing owner facts into this Process-facing contract:

- AgentFramework execution reports direct execution, local stdio MCP, and installed runtime/interpreter facts;
- application composition projects desktop-open and interactive-terminal profile facts;
- the Docker plugin projects its staged Docker dependency snapshot;
- a registered Process execution adapter projects `host.exec.managed-process-adapter`.

The snapshot is scoped. Duplicate sources for one stable ID fail closed instead of selecting a process-global winner. Capability facts can only block eligibility; they do not grant authorization.

## Process strategy contract

A process strategy/driver can declare:

- required capabilities;
- supported process/template versions;
- expected evidence/receipt types;
- preflight validation;
- recovery behavior for unavailable/failed capability.

`Processes` decides whether to:

- select the strategy;
- choose an alternative;
- block before side effects;
- escalate/recover according to process semantics.

An alternate is represented by selecting another Process strategy with a different required-capability set. Unselected strategy requirements do not block compilation. A selected driver-wide or active-strategy requirement that is absent, unavailable, unsupported, or unverified produces a deterministic Process diagnostic before execution.

The B06 runtime-tool mapping is intentionally adoption-specific:

- .NET, Python-file, PowerShell, direct command, and generic local-stdio MCP tool contracts map to their matching host facts;
- spreadsheet preview maps to `host.runtime.python` because its deterministic owner plan invokes Python;
- workspace Git contracts and `run_skill_script` map to `host.exec.direct`; B01 still resolves the concrete Git executable or extension-selected script interpreter at invocation, so capability presence cannot substitute for executable authorization;
- a browser tool backed by the exact attached local Playwright `npx` catalog entry requires `host.mcp.local-stdio`, `host.runtime.node`, and `host.runtime.node-package-manager`; a remote HTTP browser MCP does not;
- `npx` package resolution and its exact pinned package grammar remain owned and revalidated by the B04 Playwright launch boundary before any managed install directory is created;
- POSIX script, Docker, desktop-open, and interactive-terminal facts have no implicit Process runtime-tool mapping because no current Process-owned tool contract launches those surfaces. They are consumed only when a template/selected driver declares them explicitly, while B02/B05 owner services retain their own mandatory launch gates.

## Platform layer semantics

`ProcessDriverLayer.Platform` means a Process strategy package constrained by declared host capabilities. The driver catalog rejects a Platform descriptor whose driver-wide host-capability set is empty. It does not own OS detection, executable lookup, filesystem access, secrets, `System.Diagnostics.Process`, terminal discovery, or container probing. Those remain in the B01-B05 owner adapters and are projected inward as typed facts.

## Native process-start ownership inventory

The bundle-wide source guard recognizes static `Process.Start(...)` and constructed `Process` instance-start shapes. The approved production owners are:

- B01 `LocalWorkspaceProcessHost`, the canonical ordinary-process authority;
- B02 Workbench terminal presentation and Windows `runas` elevation adapters;
- A04 Linux Secret Service command runner, whose security ownership and fail-closed profile are retained by the core bundle;
- B05 FileTools desktop launcher in the direct sibling source checkout.

Formal Processes driver packages and the generic Process runtime may not create a native process, inspect the OS, access the filesystem, or resolve secrets. Module runtime-integration drivers may calculate validated logical/physical path relationships through the Core physical-path policy, but they delegate filesystem mutation/readback, native execution, and OS probing to the typed B01-B05 owner services. Docker, browser MCP, scripts, Git, desktop-open, and terminal behavior enter Processes only through those typed facts and owner ports.

## Invariants

- Canonical execution authority remains the source of workspace/tool/mutation permission.
- Host capabilities cannot widen allowed operations.
- MAF maps generic execution; it does not interpret a process failure.
- Process receipts contain logical/configured path references, stable finding identities, capability IDs, and bounded host facts; native physical paths are restricted evidence and never public receipt text.
- Effective host requirements and dispatch evidence are bounded to 32 facts, persisted across restart, and revalidated at the generic strategy-dispatch boundary before new side effects.
- `ProcessDriverLayer.Platform` is a process strategy layer, not a generic OS utility layer.
- Snapshot/profile facts participate in the immutable Process plan hash, while timestamps, physical paths, adapter messages, and secrets do not.
