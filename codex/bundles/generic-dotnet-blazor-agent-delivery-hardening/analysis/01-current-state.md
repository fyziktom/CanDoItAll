# Current State

- The previous calculator-specific build skill is not present in the seed manifest.
- `blazor-ssr-delivery` exists as a generic Blazor SSR inline skill, but still contains sample-shaped converter/unit examples in several rules.
- `.NET Application Developer`, `.NET QA Review Lead`, and `Programming Workspace Analyst` are generic enough to scaffold and validate .NET projects, but there is no dedicated Blazor implementation agent.
- `workspace_dotnet_run` is already recognized by `AgentToolInvocationPolicy` as a validation tool, but it is not implemented by `IWorkspaceCommandExecutionService`, `WorkspaceCommandPlanBuilder`, `WorkspaceRuntimePlugin`, `ToolCapabilityBuilder`, or the seeded capability catalog.
- QA guidance currently includes a PowerShell helper fallback for launching Blazor apps because the dedicated run tool is missing.
- The running web app needs a rebuild/restart after seed and tool changes before live process validation can exercise the updated catalog.
