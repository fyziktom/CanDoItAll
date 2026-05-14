# Assumptions And Risks

## Working Assumptions

- The current plugin catalog, abstractions, migrations, API, and page were added by another implementation agent and should be refactored forward rather than discarded.
- The source plugin-workflow-executors bundle remains the historical design input, but this bundle may tighten safety and sequencing based on implementation findings.
- Plugin execution stays in-process for now; this bundle creates policy enforcement and host-tool boundaries, not a true OS sandbox.
- The Docker scenario is a sample proving generic runtime contracts. Core plugin abstractions must not grow Docker-only assumptions.
- Existing workflow LLM component invocation remains the correct place to call an LLM for log summaries.

## Critical Path Risks

- If permission grants are not modeled before the workflow plugin bridge, later code will confuse "installed" and "allowed" and will be difficult to secure retroactively.
- If host-tool recipes are not typed and reviewed, a Docker plugin will become an arbitrary PowerShell launcher.
- If capability context denial is not proxy-based and fail-fast, plugins may discover broad services even when a grant is absent.
- If actor identity remains caller-supplied text, grant changes and command execution audit records cannot be trusted.
- If secrets and environment variables are not narrowed for plugin host tools, plugin processes can inherit credentials unrelated to the plugin.

## Validation Risks

- Unit tests alone can pass while UI grant toggles or workflow-editor diagnostics remain unusable.
- Browser validation can miss risk if it only opens the plugins list and does not exercise grant changes, missing-grant warnings, and connection states.
- Docker validation can become machine-dependent; tests need mocked recipe runners plus optional CLI smoke tests.
- Payload caps after workflow execution are too late to protect Docker log collection; host-command output and plugin result shaping need pre-return caps.
- EF tests can miss future N+1 or large-payload issues unless projection, paging, and artifact-storage paths are explicitly asserted.

## Reopen Triggers

- Any implementation that gives plugins raw `IWorkspaceCommandExecutionService`, raw `IServiceProvider`, arbitrary shell, arbitrary PowerShell, or unrestricted Docker arguments.
- Any implementation that treats install/enable as equivalent to user consent for files, host commands, Docker, HTTP, storage, or secrets.
- Any workflow plugin executor that runs when the plugin is disabled, connection settings are missing, or required grants are absent.
- Any UI that shows permission state but cannot actually persist and audit grant changes.
- Any Docker log path that stores large log text in EF or returns unbounded payload JSON to a workflow node.
