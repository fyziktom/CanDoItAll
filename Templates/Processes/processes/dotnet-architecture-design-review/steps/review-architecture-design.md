# Review .NET architecture design

Review the design and validation plan before implementation. Ask explicitly: is logic properly split from Blazor/components/controllers; are models and DTOs well defined and complete for the user stories; do services expose the functions needed for acceptance criteria; are functions testable without full UI/runtime; does every available criterion or invariant have an executable proof plan at the correct production boundary; are persistence, integration, security, and deployment boundaries clear; is runtime command and screenshot applicability known; and are risks or trade-offs recorded. Do not implement code or mutate product files.

## Contract
- Inputs: Architecture draft, acceptance-driven validation plan, application classification, and available scope/acceptance evidence from project-structure context or upstream artifacts.
- Outputs: Reviewed architecture decision with required implementation constraints, approval rationale, or hard block reason.
- Evidence: Checklist answers, design risks, testability assessment, implementation constraints, and go/no-go architecture recommendation.
- Operation target scope: `ExternalProductTargetReadOnly`

## Disposition Rules
- Complete the step when findings are actionable by the implementation slice. Record them as mandatory implementation constraints in the review artifact.
- Hard-block only when required input artifacts are missing, the app type or product root is contradictory, ownership is unclear, or no implementable architecture can be handed off.
- Do not use `WaitingApproval`, `NeedsManager`, or a blocked outcome only because the design needs normal implementation details such as timer cancellation, accessibility hooks, route-level browser proof, service naming, or test seams.
- Treat project-structure launch context, node title/notes/status, `ProjectStructureContextSummary`, classification findings, and draft assumptions as valid scope/acceptance evidence when no separate user-story artifact exists.
- Do not hard-block solely because a standalone acceptance-criteria or user-story file is absent; complete with explicit assumptions and mandatory implementation constraints when the available scope is implementable.
