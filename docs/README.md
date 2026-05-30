# CanDoItAll Documentation

This folder contains current operational and architecture documentation for the repository. Historical bundle folders remain in the repo for execution traceability, but current contributor guidance should start here.

## Architecture

- [Architecture beta](architecture-beta.md): current source-grounded architecture with GitHub-safe Mermaid flowcharts, C4, class, and sequence diagrams.
- [Architecture index](../architecture/README.md): ADRs, historical reviews, and current architecture entry points.

## Enterprise And Product Orientation

These docs are for readers who need to understand what CanDoItAll does before they need source-level details.

- [Enterprise operating system](enterprise-operating-system.md): customer-facing explanation of CanDoItAll as an operating system for projects, with audience-specific infographics.

## Runtime, API, And MCP

- [Root quick start](../README.md#quick-start-with-postgresql-and-qdrant): public entry point for PostgreSQL, Qdrant, build, test, and setup script commands.
- [API control plane](api-control-plane.md): current HTTP API surface for projects, project structure, processes, agents, and API access.
- [Development runtime](development-runtime.md): default PostgreSQL/Qdrant setup for Visual Studio and local Development runs.
- [PostgreSQL runtime canonicality](postgresql-runtime-canonicality.md): source-of-truth, lease finalization, and bounded parallelism rules.
- [Cognitive Memory](cognitive-memory/README.md): current implementation stage, architecture, API, validation, and roadmap for the Cognitive Memory module.
- [Processes MCP transition note](processes-mcp-setup.md): retired/suppressed MCP guidance and current replacement path.
- [Project Structure MCP transition note](project-structure-mcp-setup.md): retired/suppressed MCP guidance and current replacement path.
- [DotNetWatch persistent backend benefits](mcp-dotnetwatch-persistent-backend-benefits.md): development-sidecar runtime notes.
- [Process agent operator runbook](process-agent-operator-runbook.md): operational triage for escalations, approvals, rework, and recovery.
- [Agent output contracts](agent-output-contracts.md): typed structured-output and finalizer-tool contracts for machine-critical agent decisions.
- [Workflow MAF hardening](workflow-maf-hardening.md): workflow authoring, MAF runtime boundary, executor approval policy, and deterministic validation guidance.

## Setup Scripts

- `tools\Install-CanDoItAllWebApp.ps1`: publishes the web app as a local Windows install with launcher and desktop shortcut.
- `tools\Reinstall-CanDoItAllMcps.ps1`: rebuilds MCP projects from the sibling `CanDoItAll.Mcp` repo, updates Codex/VS Code MCP config, prepares DotNetWatch tray support, syncs skills from this repo, and removes stale retired MCP config sections.
- `codex\scripts\install-candoitall-skills.ps1`: installs repo-managed CanDoItAll skills plus required public sibling skills.

## Components And UI

- [UI shared components](ui-shared-components/README.md): current shared Blazor component-library shape, usage rules, and component references.
- [Shared components governance](shared-components-governance.md): ownership and change-request boundaries for shared UI libraries.

## Prompts And Skills

- [Prompt library implementation prompts](prompt-library-implementation-prompts.md)
- [Prompt library integration checklist](prompt-library-integration-checklist.md)
- [Portable Codex skill pack](../codex/README.md)

## Templates

- [Template workspace](../Templates/README.md)
- [Process template pack](../Templates/Processes/README.md)
