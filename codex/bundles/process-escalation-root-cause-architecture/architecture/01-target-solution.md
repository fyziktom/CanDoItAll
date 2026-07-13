# Target Solution

## End State

The process runtime can explain blocked work with typed diagnostics, determine step capability readiness before dispatch, and delegate domain-specific recovery to process drivers. Generic runtime remains reusable for enterprise processes outside software delivery, while .NET delivery and UI/browser proof behavior are isolated in domain templates and driver strategies with unit tests.

## Boundary Principles

- Generic runtime owns process state transitions, result submission, receipts, artifact lineage primitives, diagnostic persistence, and domain-neutral failure categories.
- Application services own orchestration and composition of generic runtime operations, but should delegate classification, readiness, projection enrichment, and retry/recovery decisions to small testable collaborators.
- Process drivers own domain-specific recovery playbooks and step-specific completion rules.
- MAF runtime owns agent context assembly and capability policy enforcement, but consumes process-provided typed scope instead of inferring domain behavior from prompt text.
- Templates provide domain instructions and launch variables, but should not be the only source of capability contracts or validation policy.

## Required Foundation

- Persist typed `StrategyResultEnvelope` diagnostics or a safe normalized equivalent beside result receipts.
- Project blocked diagnostic summaries into process read models and APIs.
- Represent step capability readiness as a typed contract, not as loose prompt text.
- Resolve capability readiness during launch/matching and immediately before dispatch.
- Provide domain-neutral failure categories for manager fallback.

## Domain-Specific Isolation

- .NET scaffold, restore, build, test, run, and browser-proof rules must live in .NET/software-delivery driver policy and templates.
- Visual automation/screenshot requirements must live in UI-visible process steps or visual automation drivers.
- Calculator and Tetris must remain test fixtures only.
- The generic `WorkspaceImageAnalysisPromptNormalizer` must continue to avoid assuming images are UI designs, screenshots, or software artifacts.

## Performance Shape

- Use immutable descriptors, normalized value objects, and singleton/scoped catalogs where possible.
- Do not create a new heavy driver graph per step when a reusable catalog plus per-run immutable context is sufficient.
- Readiness classification should be pure or near-pure and unit-testable.
- Projection enrichment should batch reads and avoid per-step database round trips.

## Proof Strategy

- Characterization tests first: prove current diagnostics and readiness gaps.
- Unit tests next: classifiers, readiness resolver, scope normalizer, recovery policy selection.
- Integration tests after that: launch preview, assignment persistence, projection readback, MAF capability composition.
- End-to-end replay last: simple .NET delivery, browser proof when required, and management-only step with development suppression.
