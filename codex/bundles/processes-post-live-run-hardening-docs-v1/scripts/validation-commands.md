# Validation Commands

This file is the SB15 proof-harness catalog. It splits final validation into named suites so SB18 does not depend on one timeout-prone mega command.

## Operating Rules

- Run from `C:\repositories\CanDoItAll`.
- Use an isolated output directory per suite because a live `CanDoItAll.Web.exe` can lock normal `bin` outputs.
- For integration suites that load process templates from the repository, pass `-p:CopyRepositoryTemplatesToOutput=false`.
- Build once per suite output path, then rerun the same suite with `--no-build` for proof transcripts.
- Store command output under `bundle://proof/SB15/transcripts/` or the downstream subbundle proof folder that owns the behavior.
- Treat browser and live/PostgreSQL suites as explicit opt-in proof. Do not mix them into default smoke validation.
- A quarantined suite is never release-closure proof unless the transcript names the environment, reason, and owner.

## Suite Catalog

| Suite ID | Project | Timeout-risk class | Default closure? | Transcript file | Purpose |
| --- | --- | --- | --- | --- | --- |
| `SB15-UNIT-POLICY` | `tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` | Medium | Yes | `proof/SB15/transcripts/unit-policy.txt` | Agent tool policy, MAF capability reflection, and process-adjacent unit guardrails. |
| `SB15-INTEGRATION-RUNTIME-ARTIFACTS` | `tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj` | High | Yes, split | `proof/SB15/transcripts/integration-runtime-artifacts.txt` | Runtime artifact status, dispatch, manager resolution, recovery, and finalizer slices. |
| `SB15-INTEGRATION-TEMPLATES-API` | `tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj` | High | Yes, split | `proof/SB15/transcripts/integration-templates-api.txt` | Template governance, process API parity, and baseline/live-run profile contract proof. |
| `SB15-INTEGRATION-PROCESS-API-MAF` | `tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj` | Medium | Yes, split | `proof/SB15/transcripts/integration-process-api-maf.txt` | Focused process API/OpenAPI and MAF process tool-surface proof. |
| `SB15-COMPONENT-PROCESS` | `tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj` | High if broad, medium when split | Yes, when UI changed | `proof/SB15/transcripts/component-process.txt` | Process run-step preflight and operator-console component proof. |
| `SB15-LIVE-POSTGRES` | `tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj` | Live/slow | Opt in | owning proof folder | PostgreSQL-backed scenarios such as business-plan runtime execution. |
| `SB15-BROWSER` | Browser plugin | Browser/live | Opt in | owning proof folder plus screenshot | Rendered route validation for changed UI surfaces. |
| `SB15-STATIC-AUDITS` | `rg` | Low | Yes | `proof/SB15/transcripts/static-audits.txt` | SQLite drift, stale artifact states, manager resolution, output grounding, and proof-harness source assertions. |

## Default Command Set

Build with isolated output roots before using `--no-build`:

```powershell
dotnet restore CanDoItAll.slnx
dotnet build tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -p:OutputPath=C:\repositories\CanDoItAll\artifacts\codex-sb15-unit\
dotnet build tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -p:OutputPath=C:\repositories\CanDoItAll\artifacts\codex-sb15-integration\ -p:CopyRepositoryTemplatesToOutput=false
dotnet build tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj -p:OutputPath=C:\repositories\CanDoItAll\artifacts\codex-sb15-components\
```

Run named suites separately:

```powershell
# SB15-UNIT-POLICY
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~Maf16CapabilityReflectionTests|FullyQualifiedName~WorkspaceCommandExecutionServiceTests" -p:OutputPath=C:\repositories\CanDoItAll\artifacts\codex-sb15-unit\ --logger "console;verbosity=normal"

# SB15-INTEGRATION-RUNTIME-ARTIFACTS
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests.Runtime_read_model|FullyQualifiedName~ProcessRunAutomationDispatchServiceTests.ProcessManagerAgentResolver|FullyQualifiedName~ProcessRuntimeOperatorReadModelTests|FullyQualifiedName~ProcessesServiceIntegrationTests.Blocked_transition" -p:OutputPath=C:\repositories\CanDoItAll\artifacts\codex-sb15-integration\ -p:CopyRepositoryTemplatesToOutput=false --logger "console;verbosity=normal"

# SB15-INTEGRATION-TEMPLATES-API
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessTemplateGovernanceTests" -p:OutputPath=C:\repositories\CanDoItAll\artifacts\codex-sb15-integration\ -p:CopyRepositoryTemplatesToOutput=false --logger "console;verbosity=normal"

# SB15-INTEGRATION-PROCESS-API-MAF
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ApiIntegrationTests.Api_live_run_profiles_expose_fresh_run_policy_contract|FullyQualifiedName~ApiIntegrationTests.Api_openapi_exposes_focused_control_plane_routes|FullyQualifiedName~MafAgentRuntimeTests.CreateCapabilityState_attaches_internal_process_tools_by_default_when_workspace_services_are_available|FullyQualifiedName~MafAgentRuntimeTests.Internal_process_mutation_tools_remain_available_when_approval_requirements_are_suppressed" -p:OutputPath=C:\repositories\CanDoItAll\artifacts\codex-sb15-integration\ -p:CopyRepositoryTemplatesToOutput=false --logger "console;verbosity=normal"

# SB15-COMPONENT-PROCESS
dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-build --filter "FullyQualifiedName~ProcessWorkspaceTests.Run_steps_dialog_SB15_INV_001_exposes_contract_branch_and_recovery_diagnostics_for_ui_preflight|FullyQualifiedName~ProcessWorkspaceTests.Runs_operator_console_surfaces_escalation_rework_and_timeline_controls|FullyQualifiedName~ProcessWorkspaceTests.Runs_operator_console_SB13_INV_001_surfaces_invariant_diagnostics_and_recommended_action" -p:OutputPath=C:\repositories\CanDoItAll\artifacts\codex-sb15-components\ --logger "console;verbosity=normal"
```

## Opt-In Live And Browser Suites

```powershell
# SB15-LIVE-POSTGRES
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~BusinessPlanProcessPostgresIntegrationTests" -p:OutputPath=C:\repositories\CanDoItAll\artifacts\codex-sb15-integration\ -p:CopyRepositoryTemplatesToOutput=false --logger "console;verbosity=normal"

# SB15-BROWSER
# Use the Browser plugin only when a rendered route, layout, component, or canvas changed.
# Record the route, viewport, element assertions, console errors, and screenshot path in the owning proof folder.
```

## Static Audits

```powershell
rg -n "Sqlite|SQLite|UseSqlite|Migrations.Sqlite" src tests Templates codex -S
rg -n "StaleOrWrongRun|WrongProducerMode|ContentHashMismatch|ContentUnavailable|ProjectionFailed|live-run profiles|manager chat|output grounding" src Templates codex docs -S
$qualityPattern = 'TO' + 'DO|' + 'Not' + 'Implemented|' + 'st' + 'ub|' + 'fa' + 'ke|' + 'hard' + '-code|' + 'hard' + 'coded'
rg -n $qualityPattern codex\bundles\processes-post-live-run-hardening-docs-v1\scripts\validation-commands.md tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj
```

## Quarantine Policy

- Tests with `Category=Quarantined`, `Category=LongRunning`, or `Category=LiveProcess` are excluded from default closure.
- Live/PostgreSQL proof may be used when the transcript records environment readiness and cleanup.
- Browser proof must include element assertions and console-error results, not only a screenshot.
- If a suite times out, split the filter by class or behavior slice and record the timeout as failing-first evidence instead of rerunning a broader command.
