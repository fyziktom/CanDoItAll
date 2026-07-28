# CanDoItAll Documentation

This index covers maintained contributor, architecture, runtime, and product documentation. Source code, project files, runtime composition, configuration, and endpoint mapping remain authoritative when a prose statement conflicts with implementation.

Historical execution bundles under `codex/bundles`, `.codex/bundles`, and the checked-in `codex/skills` mirror are retained for traceability. They are evidence snapshots, not current product or contributor guidance.

## Start Here

- [Repository overview](../README.md): ownership, prerequisites, quick start, build, tooling, and publication status
- [Development runtime](development-runtime.md): PostgreSQL, local data roots, Memory defaults, and readiness checks
- [Testing](testing.md): stable Release gate, extended categories, and sibling prerequisites
- [API control plane](api-control-plane.md): current HTTP API families and authorization behavior
- [Architecture beta](architecture-beta.md): source-grounded system overview
- [Contributing](../CONTRIBUTING.md): approved-partner workflow and validation expectations
- [Security policy](../SECURITY.md): supported line and private vulnerability reporting

## Architecture And Runtime Boundaries

- [Processes, MAF, and providers implementation map](processes-maf-providers-implementation-map.md)
- [Agent runtime tool surface](agent-runtime-tool-surface.md)
- [Agent execution activity and runtime snapshots](architecture/agent-execution-activity-and-runtime-snapshots.md)
- [Reusable floating agent chats](architecture/reusable-floating-agent-chats.md)
- [Process blocked-run recovery](architecture/process-blocked-run-recovery.md)
- [Project planning analytics and agent access](architecture/project-planning-analytics-and-agent-access.md)
- [Project Structure and Gantt integration](architecture/project-structure-gantt-integration.md)
- [Prompt Gallery consolidation](architecture/prompt-gallery-consolidation.md)
- [CRM/HR assignments workspace](architecture/crm-hr-assignments-workspace.md)
- [CRM/HR recruiting assessments and execution summaries](architecture/crm-hr-recruiting-assessments-and-execution-summaries.md)
- [HR agent governance](architecture/hr-agent-governance.md)

## Operations, API, And Integration

- [PostgreSQL runtime canonicality](postgresql-runtime-canonicality.md)
- [Process agent operator runbook](process-agent-operator-runbook.md)
- [CRM/HR API](crm-hr-api.md)
- [Secure configuration](secure-configuration.md)
- [OAuth email plugins](oauth-email-plugins.md)
- [Provider capability and pricing](provider-capability-and-pricing.md)
- [Processes MCP transition](processes-mcp-setup.md)
- [Project Structure MCP transition](project-structure-mcp-setup.md)
- [DotNetWatch development integration](dotnetwatch-development-integration.md)

## AgentFramework And MAF

- [MAF 1.15 compatibility](maf-1.15-compatibility.md)
- [MAF runtime stabilization](maf-runtime-stabilization.md)
- [Workflow MAF hardening](workflow-maf-hardening.md)
- [Agent output contracts](agent-output-contracts.md)

## Memory

The active implementation is the provider-neutral Memory subsystem. Native Cognitive Memory is an external service owned by its standalone repository; the legacy API path is a retired compatibility shim.

- [Memory overview and migration boundary](cognitive-memory/README.md)
- [Current implementation map](cognitive-memory/current-state/implementation-map.md)
- [Provider setup](cognitive-memory/operations/provider-setup.md)
- [Agent Memory](cognitive-memory/operations/agent-memory.md)
- [Provider authoring](cognitive-memory/operations/provider-authoring.md)
- [Legacy main-database retirement](cognitive-memory/operations/legacy-main-db-retirement.md)
- [Memory test-suite ownership](cognitive-memory/operations/memory-test-suite-rebalance.md)
- [Validation and testing](cognitive-memory/operations/validation-and-testing.md)

## Product And UI

- [Enterprise operating system](enterprise-operating-system.md)
- [UI support scope](ui-support-scope.md)
- [Shared UI consumption boundary](ui-shared-components/README.md)
- [CRM/HR recruiting assessment user stories](crm-hr-recruiting-assessment-user-stories.md)

## Repository Assets

- [Portable Codex skill installer](../codex/README.md)
- [Application templates](../Templates/README.md)
- [Process templates](../Templates/Processes/README.md)
- [Application Tailwind workspace](../Tailwind/README.md)
- [Optional Ollama context probe](../tools/ollama/README.md)

When removing or renaming a maintained document, update this index and run:

```powershell
& .\tools\Validation\Test-Documentation.ps1
```
