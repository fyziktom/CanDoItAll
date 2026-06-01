# Review .NET architecture design

Review the design before implementation. Ask explicitly: is logic properly split from Blazor/components/controllers; are models and DTOs well defined and complete for the user stories; do services expose the functions needed for acceptance criteria; are functions testable without full UI/runtime; are persistence, integration, security, and deployment boundaries clear; is runtime command and screenshot applicability known; and are risks or trade-offs recorded. Do not implement code or mutate product files.

## Contract
- Inputs: Architecture draft, application classification, scope packet, and acceptance criteria.
- Outputs: Reviewed architecture decision with required fixes, approval rationale, or block reason.
- Evidence: Checklist answers, design risks, testability assessment, and go/no-go architecture recommendation.
- Operation target scope: `ExternalProductTargetReadOnly`
