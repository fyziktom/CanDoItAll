# Bundle Profiles

## Feedback

Use this profile when the source material is already close to actionable findings:

- testing feedback
- QA notes
- screenshot reviews
- docx review documents
- short issue lists
- post-implementation follow-up defects

Recommended emphasis:

- save the raw notes exactly
- normalize each finding into a concrete requirement
- group related findings into small subbundles
- capture prerequisite and dependency signals between those subbundles
- define browser and test proof
- keep architecture light but explicit

Typical root sections:

- `inputs`
- `analysis`
- `requirements`
- `architecture`
- `plan`
- `traceability`
- `shared-prompts`
- `subbundles`
- `reviews`

## Initiative

Use this profile when the task changes architecture, introduces new projects, spans many components, or needs staged migration.

Recommended emphasis:

- structure the raw prompt into constraints and risks
- inventory the affected code, assets, or dependencies
- phase the work across subbundles with strict boundaries
- model the dependency chain and critical foundations explicitly
- create templates or request workflows when future agents will repeat the pattern

Typical extra sections:

- `inventories`
- `templates`

## Decision Rule

Choose the smallest profile that still removes ambiguity:

- If the task is mostly “fix these concrete issues,” choose `feedback`.
- If the task is “design and stage a broader change program,” choose `initiative`.
- If the task starts as feedback but reveals structural migration work, upgrade to `initiative`.

Profiles select content emphasis, not evidence weight. Assign each subbundle a separate `Standard`, `Behavioral`, or `Governed` proof tier. Do not create a bundle at all for a small coherent change that has no durable coordination need.

For an existing bundle, preserve its shape when raw inputs, requirements, current state, dependencies, work units, proof/status, and closure are recoverable. Add a compatibility map instead of migrating merely to match this profile.
