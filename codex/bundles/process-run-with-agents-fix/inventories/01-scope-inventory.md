# Scope Inventory

## In Scope

- Process run startup and automation kickoff lifecycle.
- Durable process outbox behavior.
- Process automation dispatch candidate loading and completion decisions.
- Template-pack projection and template validation tests.
- Deterministic mock-agent catalog, role mapping, and settings-gated execution.
- Calculator process definition or test fixture with QA repair loop.
- Integration tests that prove the automated process flow end to end without real LLMs.

## Out Of Scope

- Building a real calculator UI/application for users.
- Repairing real LLM behavior or provider quality.
- Redesigning the whole process module UI.
- Reworking unrelated solution-wide build failures unless they block the focused validation path.
- Changing business semantics of the generic `software-delivery` template before the deterministic test flow is proven.

## Existing Known External Blockers

- Full solution build was already blocked by unrelated compile issues in `ProjectStructureToolsTests`, `ProcessTemplatePackLoaderTests`, and `CanDoItAll.ScenarioSeeder`. This bundle owns the process-template test compile issue only when it blocks process validation.
