# Codex task — PRM-F21

Implement **Conformance, field observation, and reality-alignment reviews** inside the uploaded CanDoItAll solution.

## Constraints

- Treat `CanDoItAll.Modules.Processes` as the canonical owner for process-management behavior.
- Reuse CRM-HR, Activity, Automation, Validation, TestLab, and Security seams where the bundle says so.
- Do not add direct compile-time dependency on the uploaded AgentFramework repo.
- Keep all code comments in English.
- Preserve buildability for the current solution layout.

## Required outputs

- Code changes for this feature
- Matching tests
- Migration updates if persistence changes
- A short implementation note describing what changed and how it was verified

## Done definition

- Reviewers can record conformance observations against runs or process versions with structured deviation reasons.
- The system can cluster repeated unofficial loops, extra handoffs, and bypass patterns from journals for owner review.
- Observation notes support restricted visibility and privacy-safe governance handling; there is no unmanaged rumor registry.
- Process owners can convert deviation clusters into approved variants, fixes, or policy-breach investigations.
- Conformance reporting can show paper-versus-reality deltas by step, interface, owner, customer segment, or project.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/ProcessConformanceModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessConformanceService.cs`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessConformancePage.razor`
- `src/CanDoItAll.Modules.Security/SecurityModels.cs`
- `src/CanDoItAll.SharedKernel/ActivityStream.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessConformanceIntegrationTests.cs`