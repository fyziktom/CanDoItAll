# Draft .NET architecture design

Draft the implementation architecture from the classification and scope. Separate UI component orchestration from application/domain logic, name models and DTOs that cover the user stories, identify services and service functions needed to satisfy acceptance criteria, define persistence/integration boundaries, and outline test seams. State the production boundary that each important behavior can be validated through so the following validation-planning step does not have to infer it. Treat project-structure launch context, node title/notes/status, `ProjectStructureContextSummary`, and classification findings as valid scope evidence when no separate user-story artifact exists; record explicit assumptions instead of blocking. Do not implement code or mutate product files.

When `ProductAcceptanceCriteriaContract` is present, preserve every criterion id, `kind`, and `required` value. Only criteria with `kind=ProductAcceptance` and `required=true` define mandatory implementation boundaries. Record `kind=DeliveryPlanning` items separately as nonblocking planning context; they require no product proof and cannot cause repair, no-go, escalation, or human reconfirmation unless a separate typed decision gate explicitly requests that decision.

## Contract
- Inputs: Application classification, available scope/user-story evidence, and project structure context.
- Outputs: Reviewable .NET architecture design draft with boundaries, service model, data model, runtime assumptions, and test strategy.
- Evidence: Design options, selected approach, models, services, boundaries, test seams, and rejected alternatives.
- Operation target scope: `ExternalProductTargetReadOnly`
