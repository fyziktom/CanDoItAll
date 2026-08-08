# Runtime nonfunctional constraints

## Security

- No shell injection path for ordinary commands.
- No automatic Unix/macOS privilege elevation.
- No name-only process termination.
- No secret values in environment receipts, process output diagnostics, agent context, or CI artifacts.
- Preserve approvals, tool policy, workspace containment, TLS, and canonical authority.

## Reliability

- Process cancellation, stream drain, and tree cleanup are bounded and actual-host tested.
- One owner disposes process hosts/registries and kept-alive leases.
- Manager recovery tolerates PID reuse, process races, unreadable metadata, and permissions.
- Watchers converge after overflow/error.

## Architecture

- MAF remains generic.
- Processes owns process-domain semantics and recovery.
- Workbench owns runtime-node plan/presentation.
- Manager owns supervision/discovery.
- MCP/plugins consume shared runtime ports.
- No duplicate process/environment/executable stack.

## Compatibility

- Existing Windows PowerShell/script nodes have a bounded compatibility path.
- Windows runtime behavior remains green.
- Optional capabilities degrade without blocking core startup.
- External dependency claims are version/profile bounded.

## Maintainability

- Typed plans and capability IDs replace formatted command strings.
- Source-code comments are English.
- OS-specific code is isolated in leaf adapters with actual-host tests.
