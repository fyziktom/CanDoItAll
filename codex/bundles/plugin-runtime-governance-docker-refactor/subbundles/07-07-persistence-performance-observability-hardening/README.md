# SB07 Persistence Performance Observability Hardening

## Status

- `Completed`

## Objective

- Review and harden EF query shape, payload handling, performance-sensitive grant checks, host-command output handling, and observability after the core runtime and Docker sample are integrated.

## Success Criteria

- Grant and connection reads use projections, `AsNoTracking`, stable ordering, paging where needed, and indexes.
- Workflow execution avoids N+1 grant queries.
- Docker logs and command output stay out of large EF JSON/text fields.
- Audit and receipts diagnose behavior without leaking secrets.

## Covered Inputs

- `N004`: analyze .NET performance.
- `N005`: analyze EF Core queries.
- `N006`: Docker logs can be large and command-heavy.
- Requirements `R020`, `R021`, `R022`, `R023`, and `R024`.

## Prerequisites

- SB03 host-tool recipes complete.
- SB04 grant/connection persistence complete.
- SB05 workflow bridge complete.
- SB06 Docker sample complete.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginCatalogServices.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Persistence\PluginInstallationRecord.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorObservability.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Commands\WorkspaceCommandEnvironmentPolicy.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Process\LocalWorkspaceProcessHost.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Commands\WorkspaceCommandReceiptWriter.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs

## Deliverables

- EF query review and fixes for plugin grants, connections, settings pages, and workflow validation/runtime.
- Index review for plugin id, connection id, capability kind, recipe id, workflow scope, enabled state, and updated timestamp.
- Run-scoped grant snapshot or bounded-query strategy for workflow execution.
- Output and artifact policy review for Docker logs and host command results.
- Audit/observability review for redaction, boundary description, receipt references, grant decision, recipe id, and truncation state.
- Targeted tests or measurements proving no obvious N+1, no large logs in EF, and no secret leakage in receipts/audit.

## Dependency Impact

- SB08 final closure depends on this phase to verify architecture quality after integration.
- Any performance or EF defect found here may require reopening SB02-SB06.

## Validation Depth

- `Performance, EF, and observability hardening`

## Implementation Steps

1. Inventory all new grant, connection, workflow plugin, Docker sample, and host-tool queries.
2. Convert read models to projections and `AsNoTracking` where mutation is not needed.
3. Add paging or bounded query limits for list surfaces.
4. Verify indexes and concurrency tokens for user-mutated records.
5. Review workflow grant checking for repeated per-node/per-capability queries.
6. Verify Docker logs and large outputs flow to artifacts/storage, not EF payload text.
7. Review audit/receipt redaction and environment variable handling.
8. Add targeted tests or measurement notes and update execution report.

## Scope Exceptions

- Do not add BenchmarkDotNet unless a measured hot path justifies it.
- Do not introduce compiled queries without evidence that normal projection/index fixes are insufficient.
- Do not redesign storage infrastructure unless large-output proof exposes a blocker.

## Do Not Do

- Do not optimize by weakening permission checks.
- Do not cache grants across users, plugins, workflows, or connections without correct invalidation.
- Do not store logs in EF for convenience.
- Do not log secret values or full environment variables.

## Acceptance Checklist

- EF reads for settings and validation use projection and no tracking where appropriate.
- Grant checks during workflow execution are bounded and documented.
- Large Docker logs are artifact-backed with bounded previews.
- Audit records and receipts include enough state to debug without secrets.
- Tests or measurements prove the highest-risk paths.

## Proof Required

- Test command and result for EF/persistence behavior.
- Test command and result for large log artifact behavior.
- Audit/receipt sample with redacted secrets and boundary metadata.
- Review note covering performance anti-pattern checklist and EF query checklist.

## Browser Validation Logging

- Route: relevant route only if this subbundle changes settings, workflow details, or observability UI.
- Viewport: large-screen pass for any changed UI.
- Playwright actions: assert pagination/loading state or audit/log metadata when browser-visible.
- Screenshots: required only for UI changes.
- Review questions: metadata must be visible without exposing secrets or huge log text.

## Progression Gate

- SB08 may start only after EF, performance, output, and observability risks are either closed or explicitly documented with blocking/non-blocking severity.

## Suggested Agent Prompt

```text
Implement SB07 only.
Review and harden persistence, performance, EF query shape, output handling, and observability after the plugin runtime and Docker sample are integrated. Do not introduce broad refactors or weaken permission checks.
```
