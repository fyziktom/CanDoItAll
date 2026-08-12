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

## B00 execution rebaseline — 2026-08-10

The prepared `62ea8ee0cc42c1c06da934d126a5c18f8237a89f` source was re-anchored to `dd78ffa9769ba1d125b8be81a4b303df37c32505` on `unix-adoption`. The accepted local-development siblings are Components `8372c1d55f21b349f8e859470b02eeb4421e96ca` and FileTools `f31e20d054003348c7557b9634e0838fc5996ae0`. All 33 prepared references still exist; four newly discovered execution references bring the current manifest to 37/37 existing paths.

The rebaseline found 17 execution-related surfaces. Twelve are production launch or recovery surfaces owned by B01–B06; one is the Processes semantic boundary; one security-native helper remains delegated to the completed core A04 scope; and two are validation-only surfaces owned by B07. No P0/P1 surface remains unclassified. The detailed records are in `inventories/runtime-surface-inventory.csv`, `inventories/process-ownership-inventory.csv`, and `inventories/executable-capability-inventory.csv`.

Newly explicit prepared-source deltas are:

- the Git runner and Windows `subst` alias process belong to B01;
- independent workspace process-host construction is a B01 lifetime defect rather than a new process-domain owner;
- Manager watch, Tailwind, and tuning launchers are separate B03 runners, with the tuning adapter retaining string-form arguments and incomplete cancellation cleanup;
- Docker constructs its own workspace process host and retains plugin-local executable/environment policy, owned by B05;
- native vault helper execution remains Security-owned and is not pulled into the runtime process abstraction.

The static portability scan covered 4,826 tracked files and produced 27,261 candidate findings for classification. Behavioral characterization is green on the same named slice on Windows and Linux: 165/165 unit plus 4/4 integration tests per host. No production source changed in B00.

## Split-trigger result

The runtime program crosses more than eight project ownership boundaries and is expected to exceed 60 production files. The existing B01–B07 decomposition already satisfies those split triggers with independent gates. B90 remains reserved for an actual architecture-gate failure and B91 for a proven external dependency regression; neither is invoked by B00. The sibling repositories are already pinned and connected through the committed local-project-reference switch, so no additional external source bundle is required at R0.
