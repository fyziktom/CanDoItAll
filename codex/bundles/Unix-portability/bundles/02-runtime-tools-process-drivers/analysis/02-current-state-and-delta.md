# Runtime current state and delta

## Positive foundations

- `LocalWorkspaceProcessHost` uses direct `ProcessStartInfo.ArgumentList`, redirected streams, timeout, and `Kill(entireProcessTree: true)`.
- `McpExecutableResolver` already distinguishes Windows PATHEXT behavior from Unix PATH behavior.
- Playwright MCP resolution already distinguishes `npm.cmd` and `npm`.
- Docker recipes build typed argument lists rather than a single shell string.
- The recent MAF refactor created narrow runtime ports and moved process recovery/semantics outward.
- Process drivers have explicit abstractions and standard descriptors.

## Primary defects

- environment variables and many path sets use case-insensitive dictionaries on every OS;
- workspace executable locator probes Windows suffixes universally;
- process-tree behavior is unproven on Unix/macOS;
- ExternalProcessToolInvoker and Docker create independent process execution paths;
- Workbench runtime-node execution is Windows/PowerShell/runas only;
- Python venv is Windows layout-specific;
- Manager ownership recovery is WMI plus insufficient Unix name matching;
- MCP policy and resolver use different identity rules;
- Playwright MCP trusts global npx cache discovery;
- secret values/outputs require stricter redaction boundaries;
- FileTools runtime behavior is outside this repository;
- no active three-platform runtime CI exists.

## Current ownership constraint

The MAF refactor ADR is authoritative: `Processes` owns process semantics and recovery. Platform capability work must not turn MAF or Infrastructure into the owner of process eligibility, strategy, evidence, or escalation.
