# 11 Regression Proof For Processes Workflows

## Status

- `Ready after SB09-SB10`

## Objective

- Prove the migration preserves agent, process, workflow, tool policy, capability access restriction, and UI behavior across unit, integration, component, and e2e layers.

## Success Criteria

- Existing tests pass or are intentionally updated with compatibility-preserving expectations.
- Process/workflow templates still resolve required capabilities and tool policies.
- Process/workflow templates can limit skills, tools, MCP servers, and MCP tools through the shared typed policy model.
- UI setup and runtime execution operate against template-backed, isolated services.
- Success and failure paths expose actionable structured diagnostics for external tools and MCPs.
- Denied required capabilities and suppressed runtime attachments are diagnosable enough for a user or agent to repair configuration.

## Covered Inputs

- R02, R06, R09, R11, R12, R13, R14, R15.
- Mandatory requirement to preserve all functionalities.

## Prerequisites

- SB09 runtime hardening proof passes.
- SB10 UI/API setup proof passes.

## Exact Source References

- `repo://Templates/Processes/manifest.json`
- `repo://Templates/Workflows/manifest.yaml`
- `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`
- `repo://tests/CanDoItAll.Tests.Components`
- `repo://tests/CanDoItAll.Tests.Playwright`
- `bundle://inventories/03-error-state-inventory.md`
- `bundle://inventories/04-capability-access-policy-test-inventory.md`

## Deliverables

- Regression test execution matrix with unit, integration, component, and Playwright results.
- Process/workflow smoke proving current templates still operate.
- Process/workflow restriction smoke proving denial of a representative skill, tool, MCP server, and MCP tool.
- Browser-visible validation of capability setup and seeded capability list.
- Negative e2e/API proof that representative external tool and MCP setup failures are repairable and masked.
- Updated execution report with failure repairs or explicit residual risks.

## Dependency Impact

- SB12 cleanup is blocked until SB11 proves no behavior regression.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run focused unit tests for naming, access policy conversion/precedence, tool policy, loaders, invokers, MCP lifecycle, and MAF adapters.
2. Run integration tests for seed materialization, capability filtering, access policy suppression, and runtime composition.
3. Run component tests for setup wizard, capability panels, and access policy editor/preview.
4. Run API negative tests for external tool failure, MCP list-tools failure, invalid policy selector, and denied required capability.
5. Run Playwright tests for setup, failed setup diagnostics, policy preview, and a representative process/workflow path with restrictions.
6. Compare failures against old behavior and repair within owning subbundle if needed.
7. Record proof artifacts with transcripts, screenshots, and trace paths.

## Scope Exceptions

- Do not perform broad unrelated refactors during regression repairs.
- Do not accept snapshot-only proof if behavior assertions are missing.

## Do Not Do

- Do not skip failing old tests by weakening assertions.
- Do not close the subbundle if process/workflow coverage only exercises template loading without runtime tool use.
- Do not close if restriction coverage only denies tools; skills, MCP servers, and MCP tools need proof too.
- Do not close if setup failure messages lack category, repair hint, or masked bounded diagnostic detail.

## Acceptance Checklist

- Seeded default catalog parity is proven.
- MAF runtime composition is proven.
- Capability setup UI proof is visually reviewed.
- Process/workflow path executes representative tool families.
- Process/workflow restrictions deny representative skill, tool, MCP server, and MCP tool through the shared evaluator.
- Denied required capability proof blocks execution with exact denying rule diagnostics.
- No leaked local MCP processes remain after tests.
- External tool and MCP failure proof is actionable enough for a user or agent to repair configuration.

## Proof Required

- Test transcripts for unit, integration, component, and Playwright suites.
- Screenshots and trace paths for UI/e2e proof.
- Negative setup diagnostics proof.
- Access policy suppression and denied-required diagnostics proof.
- `proof/SB11/manifest.md`
- `proof/SB11/semantic-invariants.md`

## Browser Validation Logging

- Target routes/windows: capability management, process/workflow launch or representative runtime screen.
- Required viewports: maximized desktop and narrower viewport for setup dialog.
- Required actions: verify seeded capabilities, run setup tests, create/preview a deny policy, run representative process/workflow smoke with restrictions, inspect evidence.
- Evidence paths: screenshots, traces, and console/network logs in `proof/SB11/manifest.md`.
- Review questions: capability counts correct, setup errors actionable, no overlapping UI, runtime proof visible.

## Progression Gate

- SB12 cleanup cannot start until SB11 proof shows behavior parity or records an explicit accepted exception.

## Suggested Agent Prompt

```text
Implement subbundle SB11 only. Run and repair regression proof across unit, integration, component, and Playwright layers. Preserve behavior and reopen earlier subbundles if proof shows the new abstractions changed capability behavior, restrictions are not generic across skills/tools/MCPs, or setup/policy failures are not repairable.
```

