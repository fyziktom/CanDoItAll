# Codex task — PRM-F18

Implement **Variants, exceptions, input quality, and decision rights** inside the uploaded CanDoItAll solution.

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

- Steps can define mandatory inputs, completeness checks, and structured rejection/rework reasons before execution continues.
- The model distinguishes normal path, approved variant, and exception path metadata with escalation or override requirements.
- Decision rights are explicit: who can decide, under what threshold or rule, with what evidence, and through which override route.
- Controls can be tagged as mandatory, conditional, or optional based on risk tier so low-risk work is not over-approved.
- Runtime journals capture exception reasons, overrides, and input-quality failures separately from generic failure states.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/ProcessInputQualityModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessExceptionServices.cs`
- `src/CanDoItAll.Modules.Processes/ProcessDecisionRightsService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessPolicyModels.cs`
- `src/CanDoItAll.Modules.Validation/*`
- `src/CanDoItAll.Modules.Security/SecurityModels.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessDecisionRightsIntegrationTests.cs`