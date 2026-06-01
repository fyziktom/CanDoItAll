# Draft .NET architecture design

Draft the implementation architecture from the classification and scope. Separate UI component orchestration from application/domain logic, name models and DTOs that cover the user stories, identify services and service functions needed to satisfy acceptance criteria, define persistence/integration boundaries, and outline test seams. Do not implement code or mutate product files.

## Contract
- Inputs: Application classification, scope packet, user stories, and project structure context.
- Outputs: Reviewable .NET architecture design draft with boundaries, service model, data model, runtime assumptions, and test strategy.
- Evidence: Design options, selected approach, models, services, boundaries, test seams, and rejected alternatives.
- Operation target scope: `ExternalProductTargetReadOnly`
