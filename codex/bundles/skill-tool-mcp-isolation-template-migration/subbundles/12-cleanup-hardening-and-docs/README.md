# 12 Cleanup Hardening And Docs

## Status

- `Completed`

## Objective

- Remove obsolete hardcoded paths, harden diagnostics, document naming/template/access-policy conventions, and close the migration with final validation.

## Success Criteria

- No active runtime or seed path silently uses old hardcoded capability defaults.
- Documentation explains how to add Skill, Tool, MCP templates, implementations, exposure descriptors, and access restrictions.
- Final validation passes with no known regressions or with explicit accepted exceptions.
- Guard tests prevent reintroduction of generic external tool/MCP error messages, hidden hardcoded fallbacks, and hidden capability suppression outside the shared evaluator.

## Covered Inputs

- R01-R15 closure.

## Prerequisites

- SB11 regression proof passes.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy`
- `repo://Templates/README.md`
- `bundle://requirements/02-naming-and-compatibility-standards.md`
- `bundle://architecture/03-error-and-diagnostics-model.md`
- `bundle://architecture/04-implementation-quality-guardrails.md`
- `bundle://architecture/05-capability-access-policy.md`

## Deliverables

- Removed or clearly deprecated hardcoded capability construction paths.
- Developer documentation for adding templates and implementations.
- Developer documentation for capability access policies, typed selector values, UI editing, process/workflow usage, and suppression diagnostics.
- Final migration notes for compatibility and managed seed versioning.
- Final validator/test report.
- Guard tests for no private MAF capability DTOs, no hidden seed fallback, no hidden runtime suppression, no raw selector string comparisons in runtime access logic, and no generic external tool/MCP setup errors.

## Dependency Impact

- This is the final cleanup and closure subbundle. Weak cleanup leaves future maintainers with two competing capability systems.

## Validation Depth

- `Final closure`

## Implementation Steps

1. Remove dead hardcoded capability builders after SB11 proves replacement.
2. Add guard tests preventing reintroduction of private MAF capability DTOs or hardcoded template defaults.
3. Add guard tests preventing hidden runtime suppression outside `EffectiveCapabilitySet` and raw selector string comparisons in runtime access logic.
4. Add guard tests preventing generic external tool/MCP setup error messages where structured diagnostics are available.
5. Document template folder structure, naming conventions, access policy conventions, setup tests, diagnostics, failure repair flow, exposure descriptor requirements, and implementation registration.
6. Update architecture docs or README files that describe old behavior.
7. Run final build/test/validator closure.

## Scope Exceptions

- Leave compatibility shims only when they are documented, tested, and scheduled for removal.
- `SandboxWorkspaceSeedBuilder.cs` remains an existing seed aggregate over 500 lines after capability cleanup; SB12 removed obsolete capability construction paths and leaves unrelated provider/agent seed splitting to a future seed-organization refactor.

## Do Not Do

- Do not delete compatibility data needed to read existing persisted catalogs.
- Do not make documentation aspirational; it must match implemented files and APIs.
- Do not keep old hardcoded code as an undocumented active alternate path.
- Do not document raw JSON/string editing as the normal path for access restrictions when typed UI/API exists.

## Acceptance Checklist

- Developers can add a new skill, internal tool, external tool, internal MCP, external MCP, and matching exposure descriptor by following docs.
- Developers can restrict a capability in agent/process/workflow templates and UI using typed selectors by following docs.
- Final tests pass.
- Final bundle execution report is complete.
- No hidden fallback to old seed builder exists.
- Documentation shows how to interpret and repair representative external tool and MCP setup failures.

## Proof Required

- Final build and test transcripts.
- Static search proving old hardcoded active paths are gone or documented compatibility adapters.
- Static search proving no hidden suppression path bypasses the shared evaluator.
- Documentation review.
- Guard test transcripts.
- `proof/SB12/manifest.md`
- `proof/SB12/semantic-invariants.md`

## Browser Validation Logging

- Use SB11 UI routes for final smoke if docs or cleanup touched visible setup behavior.
- Result: `N/A with reference to SB11 proof`; SB12 changed seed cleanup, guard tests, and documentation only, with no visible setup/process/workflow UI behavior changed.

## Progression Gate

- Bundle may close. SB12 proof and final validator passed.

## Completion Evidence

- `proof/SB12/manifest.md`
- `proof/SB12/semantic-invariants.md`
- `proof/SB12/transcripts/unit-capability-cleanup-regression.txt` (`274` passed)
- `proof/SB12/transcripts/integration-seed-filter-api-workflow-regression.txt` (`34` passed)
- `proof/SB12/transcripts/component-setup-process-workflow-regression.txt` (`60` passed)
- `proof/SB12/transcripts/dotnet-build-solution.txt` (`0` warnings, `0` errors)
- `proof/SB12/transcripts/static-cleanup-scan.txt`
- `proof/SB12/transcripts/documentation-review.txt`
- `proof/SB12/transcripts/file-size-scan.txt`
- `proof/SB12/transcripts/bundle-validator.txt`

## Suggested Agent Prompt

```text
Implement subbundle SB12 only after SB11 passes. Remove obsolete hardcoded paths, add guard tests, document the new capability template, access policy, diagnostics, and implementation workflow, and run final validation.
```

